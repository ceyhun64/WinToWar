using api;
using api.Models.Payments;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// Bölüm 3.1 uçtan uca akışı: gerçek bir SQLite veritabanına (in-memory) karşı
/// invoice oluşturma → webhook ile onaylama → idempotency + monotonluk kontrolü.
/// </summary>
public class PaymentServiceIntegrationTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly PaymentConfig _config;
    private readonly ManualTimeProvider _timeProvider;
    private readonly FakePaymentProvider _paymentProvider;
    private readonly FakeHubContext _hubContext;
    private readonly PaymentService _sut;

    public PaymentServiceIntegrationTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _config = new PaymentConfig { WebhookSecret = "test-secret", RequiredConfirmations = 1 };
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _paymentProvider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance);
        _hubContext = new FakeHubContext();

        var notifier = new PaymentEventNotifier(_hubContext);
        var refundService = new RefundService(_db, _paymentProvider, Options.Create(_config), _timeProvider, notifier, NullLogger<RefundService>.Instance);
        _sut = new PaymentService(
            _db,
            new FixedPriceOracle(44.5m),
            _paymentProvider,
            Options.Create(_config),
            _timeProvider,
            notifier,
            refundService,
            NullLogger<PaymentService>.Instance);
    }

    private const string ValidAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4";

    [Fact]
    public async Task CreateInvoice_PersistsInvoiceWithRoundedAmountAndPendingStatus()
    {
        var dto = await _sut.CreateInvoiceAsync("match-1", "player-1", ValidAddress, CancellationToken.None);

        Assert.Equal("Pending", dto.Status);
        Assert.Equal("1.00", dto.AmountUsd);
        Assert.Equal((1.00m / 44.5m).ToString("0.00000000", System.Globalization.CultureInfo.InvariantCulture), dto.AmountLtc);

        var stored = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Pending, stored.Status);
        Assert.Equal(PriceOracleSource.CoinGecko, stored.PriceOracleSource);
    }

    [Fact]
    public async Task CreateInvoice_CalledTwiceForSamePlayerAndMatch_ReturnsSameInvoice_Idempotent()
    {
        var first = await _sut.CreateInvoiceAsync("match-1", "player-1", ValidAddress, CancellationToken.None);
        var second = await _sut.CreateInvoiceAsync("match-1", "player-1", ValidAddress, CancellationToken.None);

        Assert.Equal(first.InvoiceId, second.InvoiceId);
        Assert.Equal(1, await _db.PaymentInvoices.CountAsync());
    }

    [Fact]
    public async Task InvalidPayoutAddress_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            _sut.CreateInvoiceAsync("match-1", "player-1", "not-a-real-address", CancellationToken.None));
    }

    [Fact]
    public async Task Webhook_WithValidSignatureAndSettledEvent_ConfirmsInvoice_AndNotifiesSignalR()
    {
        var invoice = await _sut.CreateInvoiceAsync("match-1", "player-1", ValidAddress, CancellationToken.None);
        var stored = await _db.PaymentInvoices.SingleAsync();

        var payload = BuildSettledPayload("evt-1", stored.BtcPayInvoiceId, stored.AmountLtc);
        var signature = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);

        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None);

        var updated = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Confirmed, updated.Status);
        Assert.NotNull(updated.ConfirmedAt);
        Assert.Single(_hubContext.Proxy.Sent);
        Assert.Equal("PaymentConfirmed", _hubContext.Proxy.Sent[0].Method);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ThrowsAndDoesNotChangeState()
    {
        var invoice = await _sut.CreateInvoiceAsync("match-1", "player-1", ValidAddress, CancellationToken.None);
        var stored = await _db.PaymentInvoices.SingleAsync();
        var payload = BuildSettledPayload("evt-1", stored.BtcPayInvoiceId, stored.AmountLtc);

        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            _sut.HandleWebhookAsync(payload, "sha256=deadbeef", CancellationToken.None));

        var unchanged = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Pending, unchanged.Status);
    }

    [Fact]
    public async Task DuplicateWebhookEvent_IsNoOp_SecondDeliveryDoesNotDoubleNotify()
    {
        await _sut.CreateInvoiceAsync("match-1", "player-1", ValidAddress, CancellationToken.None);
        var stored = await _db.PaymentInvoices.SingleAsync();
        var payload = BuildSettledPayload("evt-duplicate", stored.BtcPayInvoiceId, stored.AmountLtc);
        var signature = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);

        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None);
        await _sut.HandleWebhookAsync(payload, signature, CancellationToken.None); // aynı deliveryId, tekrar teslimat

        Assert.Single(_hubContext.Proxy.Sent); // yalnızca bir kez notify edilmiş olmalı
        Assert.Equal(1, await _db.ProcessedWebhookEvents.CountAsync(e => e.EventId == "evt-duplicate"));
    }

    [Fact]
    public async Task OutOfOrderWebhook_AfterConfirmed_IsIgnored_StateStaysConfirmed()
    {
        await _sut.CreateInvoiceAsync("match-1", "player-1", ValidAddress, CancellationToken.None);
        var stored = await _db.PaymentInvoices.SingleAsync();

        var settlePayload = BuildSettledPayload("evt-settle", stored.BtcPayInvoiceId, stored.AmountLtc);
        await _sut.HandleWebhookAsync(settlePayload, WebhookSignatureValidator.ComputeSignatureHeader(settlePayload, _config.WebhookSecret), CancellationToken.None);

        // Gecikmeli, farklı bir event id'siyle gelen ikinci bir "Settled" event'i — aynı rank, forward değil.
        var lateDuplicate = BuildSettledPayload("evt-late-duplicate", stored.BtcPayInvoiceId, stored.AmountLtc);
        await _sut.HandleWebhookAsync(lateDuplicate, WebhookSignatureValidator.ComputeSignatureHeader(lateDuplicate, _config.WebhookSecret), CancellationToken.None);

        Assert.Equal(PaymentInvoiceStatus.Confirmed, (await _db.PaymentInvoices.SingleAsync()).Status);
        Assert.Single(_hubContext.Proxy.Sent); // ikinci event confirm bildirimini TEKRAR tetiklememeli.
    }

    private string BuildSettledPayload(string deliveryId, string btcPayInvoiceId, decimal amountLtc) =>
        $$"""
        {
          "deliveryId": "{{deliveryId}}",
          "type": "InvoiceSettled",
          "timestamp": {{_timeProvider.GetUtcNow().ToUnixTimeSeconds()}},
          "invoiceId": "{{btcPayInvoiceId}}",
          "confirmations": 1,
          "paidAmountLtc": "{{amountLtc.ToString("0.00000000", System.Globalization.CultureInfo.InvariantCulture)}}"
        }
        """;

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
