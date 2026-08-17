using api;
using api.Models.Payments;
using RoomModels = api.Models.Rooms;
using api.Services;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// Bölüm 3.1 uçtan uca akışı: gerçek bir SQLite veritabanına (in-memory) karşı
/// invoice oluşturma → webhook ile onaylama → idempotency + monotonluk kontrolü.
/// docs/05-payment.md Bölüm 1.9: match-entry invoice'ları artık gerçek bir Room/Match
/// gerektirir (tutar sunucuda Room.EntryFeeUsd - Wallet.BalanceUsd olarak hesaplanır).
/// </summary>
public class PaymentServiceIntegrationTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly PaymentConfig _config;
    private readonly ManualTimeProvider _timeProvider;
    private readonly ConfigurableTotalPaidPaymentProvider _paymentProvider;
    private readonly FakeHubContext _hubContext;
    private readonly PaymentService _sut;
    private readonly string _matchId;

    public PaymentServiceIntegrationTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _config = new PaymentConfig { WebhookSecret = "test-secret", RequiredConfirmations = 1 };
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _paymentProvider = new ConfigurableTotalPaidPaymentProvider();
        _hubContext = new FakeHubContext();

        var notifier = new PaymentEventNotifier(_hubContext, new FakeWalletHubContext());
        var mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        var matchManager = new MatchManager(mapProvider, TestEventLog.Writer(), NullLogger<MatchManager>.Instance);
        var priceOracle = new FixedPriceOracle(44.5m);
        var walletService = new WalletService(_db, priceOracle, _paymentProvider, Options.Create(_config), _timeProvider, notifier, NullLogger<WalletService>.Instance);
        var refundService = new RefundService(_db, walletService, _timeProvider, NullLogger<RefundService>.Instance);
        var roomEntryService = new RoomEntryService(matchManager, walletService, NullLogger<RoomEntryService>.Instance);

        var room = new RoomModels.Room
        {
            Id = "room-1",
            Type = RoomModels.RoomType.Standard,
            MaxPlayers = 4,
            GreyRegionDefenseCount = 1,
            FogOfWar = false,
            EntryFeeUsd = 1.00m,
            CreatorPlayerId = "creator"
        };
        var match = matchManager.CreateMatch(room, DateTimeOffset.UtcNow.UtcDateTime);
        _matchId = match.Id;

        _sut = new PaymentService(
            _db,
            priceOracle,
            _paymentProvider,
            Options.Create(_config),
            _timeProvider,
            notifier,
            refundService,
            walletService,
            roomEntryService,
            matchManager,
            NullLogger<PaymentService>.Instance);
    }

    private const string PlayerName = "Alice";

    private Task<Models.Payments.Dtos.PaymentInvoiceDto> CreateInvoice(string playerId = "player-1") =>
        _sut.CreateMatchEntryInvoiceAsync(_matchId, playerId, PlayerName, CancellationToken.None);

    [Fact]
    public async Task CreateInvoice_PersistsInvoiceWithRoundedAmountAndPendingStatus()
    {
        var dto = await CreateInvoice();

        Assert.Equal("Pending", dto.Status);
        Assert.Equal("1.00", dto.AmountUsd);
        Assert.Equal((1.00m / 44.5m).ToString("0.00000000", System.Globalization.CultureInfo.InvariantCulture), dto.AmountLtc);

        var stored = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Pending, stored.Status);
        Assert.Equal(PriceOracleSource.CoinGecko, stored.PriceOracleSource);
    }

    /// <summary>
    /// Regresyon: `ReceivingAddress`/`Bip21Uri` invoice satırında kalıcılaşmalı —
    /// önceden yalnızca oluşturma anındaki DTO'da vardı, `GetInvoiceAsync` (bu
    /// sayfanın 3 saniyede bir polling ile çağırdığı uç) her zaman boş string
    /// döndürüyordu, bu yüzden `/odeme/[invoiceId]` sayfasında adres ilk yükten
    /// hemen sonra kayboluyordu.
    /// </summary>
    [Fact]
    public async Task CreateInvoice_ThenGetInvoiceAsync_StillReturnsReceivingAddressAndBip21Uri()
    {
        var created = await CreateInvoice();
        Assert.NotEmpty(created.ReceivingAddress);
        Assert.NotEmpty(created.Bip21Uri);

        var fetched = await _sut.GetInvoiceAsync(Guid.Parse(created.InvoiceId), CancellationToken.None);

        Assert.Equal(created.ReceivingAddress, fetched.ReceivingAddress);
        Assert.Equal(created.Bip21Uri, fetched.Bip21Uri);
    }

    [Fact]
    public async Task CreateInvoice_CalledTwiceForSamePlayerAndMatch_ReturnsSameInvoice_Idempotent()
    {
        var first = await CreateInvoice();
        var second = await CreateInvoice();

        Assert.Equal(first.InvoiceId, second.InvoiceId);
        Assert.NotEmpty(second.ReceivingAddress);
        Assert.Equal(1, await _db.PaymentInvoices.CountAsync());
    }

    [Fact]
    public async Task Webhook_WithValidSignatureAndSettledEvent_ConfirmsInvoice_AndNotifiesSignalR()
    {
        await CreateInvoice();
        var stored = await _db.PaymentInvoices.SingleAsync();

        var payload = BuildSettledPayload("evt-1", stored.BtcPayInvoiceId);
        var signature = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);

        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None);

        var updated = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Confirmed, updated.Status);
        Assert.NotNull(updated.ConfirmedAt);
        Assert.Equal(MatchJoinOutcome.Joined, updated.MatchJoinOutcome);
        Assert.Single(_hubContext.Proxy.Sent);
        Assert.Equal("PaymentConfirmed", _hubContext.Proxy.Sent[0].Method);
    }

    /// <summary>
    /// Regresyon guard'ı: docs/14-payment-sandbox.md'deki gerçek regtest E2E
    /// bulgusu — gerçek BTCPay "InvoiceSettled" webhook'unda üst seviyede
    /// "confirmations" alanı hiç bulunmuyor. Bu test, payload'da bu alan
    /// TAMAMEN YOKKEN (paylaşılan BuildSettledPayload fixture'ından bağımsız,
    /// kasıtlı olarak elle yazılmış minimal gerçek şekil) invoice'ın yine de
    /// Confirmed'e geçtiğini kanıtlar — signature + idempotency + forward-
    /// transition kontrolleri tek başına yeterli olmalı.
    /// </summary>
    [Fact]
    public async Task Webhook_RealBtcPayShapeWithoutConfirmationsField_StillConfirmsInvoice()
    {
        await CreateInvoice();
        var stored = await _db.PaymentInvoices.SingleAsync();

        var payload = $$"""
            {
              "manuallyMarked": false,
              "overPaid": false,
              "deliveryId": "evt-real-shape",
              "type": "InvoiceSettled",
              "timestamp": {{_timeProvider.GetUtcNow().ToUnixTimeSeconds()}},
              "invoiceId": "{{stored.BtcPayInvoiceId}}"
            }
            """;
        var signature = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);

        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None);

        var updated = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Confirmed, updated.Status);
        Assert.NotNull(updated.ConfirmedAt);
    }

    /// <summary>
    /// docs/15-payment-flow-verification.md gerçek regtest E2E bulgusu: gerçek bir
    /// invoice'ı fazla ödeyip mine edildiğinde, webhook payload'ında ödenen tutar
    /// bilgisi hiç gelmediği için eski kod overpayment'ı asla algılamıyordu (fazlalık
    /// sessizce store'a gidiyordu, oyuncuya hiç iade edilmiyordu). Düzeltme sonrası
    /// tutar artık IPaymentProvider.GetTotalPaidLtcAsync'ten okunuyor — bu test bunu
    /// bir birim testiyle sabitler (ConfigurableTotalPaidPaymentProvider ile).
    /// </summary>
    [Fact]
    public async Task Webhook_RealPaidAmountFromProviderExceedsToleranceAndThreshold_RefundsOverpaymentToWallet()
    {
        var dto = await _sut.CreateTopUpInvoiceAsync("player-1", 10.00m, CancellationToken.None);
        var stored = await _db.PaymentInvoices.SingleAsync();

        // Oyuncu invoice tutarının iki katını gönderdi (gerçek dünyada litecoind ile
        // yapılan aşırı ödeme senaryosunun aynısı) — provider bunu simüle eder.
        _paymentProvider.TotalPaidLtc = stored.AmountLtc * 2;

        var payload = BuildSettledPayload("evt-overpay", stored.BtcPayInvoiceId);
        var signature = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);
        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None);

        var refund = await _db.Refunds.SingleAsync();
        Assert.Equal(RefundReason.Overpayment, refund.Reason);
        Assert.True(refund.AmountUsd > 9.00m && refund.AmountUsd < 10.00m, $"Beklenmeyen refund tutarı: {refund.AmountUsd}");

        // AsNoTracking: CreditAsync/RefundService.SubmitAsync ExecuteUpdateAsync ile doğrudan
        // SQL UPDATE çalıştırır (change tracker'ı bypass eder) — aynı DbContext'te izlenen
        // (tracked) bir Wallet varsa (EnsureWalletRowExistsAsync'in Add'i) EF identity map'i
        // sorguda güncel DB değeri yerine bayat izlenen değeri döndürür, bu yüzden burada
        // taze veri için AsNoTracking zorunludur.
        var wallet = await _db.Wallets.AsNoTracking().SingleAsync(w => w.PlayerId == "player-1");
        Assert.Equal(decimal.Parse(dto.AmountUsd, System.Globalization.CultureInfo.InvariantCulture) + refund.AmountUsd, wallet.BalanceUsd);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ThrowsAndDoesNotChangeState()
    {
        await CreateInvoice();
        var stored = await _db.PaymentInvoices.SingleAsync();
        var payload = BuildSettledPayload("evt-1", stored.BtcPayInvoiceId);

        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            _sut.HandleWebhookAsync(payload, "sha256=deadbeef", CancellationToken.None));

        var unchanged = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Pending, unchanged.Status);
    }

    [Fact]
    public async Task DuplicateWebhookEvent_IsNoOp_SecondDeliveryDoesNotDoubleNotify()
    {
        await CreateInvoice();
        var stored = await _db.PaymentInvoices.SingleAsync();
        var payload = BuildSettledPayload("evt-duplicate", stored.BtcPayInvoiceId);
        var signature = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);

        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None);
        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None); // aynı deliveryId, tekrar teslimat

        Assert.Single(_hubContext.Proxy.Sent); // yalnızca bir kez notify edilmiş olmalı
        Assert.Equal(1, await _db.ProcessedWebhookEvents.CountAsync(e => e.EventId == "evt-duplicate"));
    }

    [Fact]
    public async Task OutOfOrderWebhook_AfterConfirmed_IsIgnored_StateStaysConfirmed()
    {
        await CreateInvoice();
        var stored = await _db.PaymentInvoices.SingleAsync();

        var settlePayload = BuildSettledPayload("evt-settle", stored.BtcPayInvoiceId);
        await _sut.HandleWebhookAsync(settlePayload, WebhookSignatureValidator.ComputeSignatureHeader(settlePayload, _config.WebhookSecret), CancellationToken.None);

        // Gecikmeli, farklı bir event id'siyle gelen ikinci bir "Settled" event'i — aynı rank, forward değil.
        var lateDuplicate = BuildSettledPayload("evt-late-duplicate", stored.BtcPayInvoiceId);
        await _sut.HandleWebhookAsync(lateDuplicate, WebhookSignatureValidator.ComputeSignatureHeader(lateDuplicate, _config.WebhookSecret), CancellationToken.None);

        Assert.Equal(PaymentInvoiceStatus.Confirmed, (await _db.PaymentInvoices.SingleAsync()).Status);
        Assert.Single(_hubContext.Proxy.Sent); // ikinci event confirm bildirimini TEKRAR tetiklememeli.
    }

    /// <summary>
    /// docs/14-payment-sandbox.md gerçek regtest E2E bulgusu: gerçek BTCPay
    /// Greenfield "InvoiceSettled" webhook'u bu şekli taşır — üst seviyede ne
    /// "confirmations" ne de "paidAmountLtc" alanı vardır (yalnızca event
    /// zarfı + metadata). Önceki sürüm bu iki alanı (gerçekte hiç var olmayan
    /// bir şekilde) kendisi üretiyordu; bu, kodun asla gerçek bir BTCPay'e
    /// karşı çalıştırılmadan yazıldığını gizliyordu.
    /// </summary>
    private string BuildSettledPayload(string deliveryId, string btcPayInvoiceId) =>
        $$"""
        {
          "manuallyMarked": false,
          "overPaid": false,
          "deliveryId": "{{deliveryId}}",
          "webhookId": "wh_test",
          "originalDeliveryId": "{{deliveryId}}",
          "isRedelivery": false,
          "type": "InvoiceSettled",
          "timestamp": {{_timeProvider.GetUtcNow().ToUnixTimeSeconds()}},
          "storeId": "store_test",
          "invoiceId": "{{btcPayInvoiceId}}",
          "metadata": {
            "matchId": null,
            "playerId": "player-1"
          }
        }
        """;

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
