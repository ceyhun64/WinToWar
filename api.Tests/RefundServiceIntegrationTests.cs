using api.Models.Payments;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// 2026-08-08 kararı: refund artık on-chain gönderim değil, senkron bir
/// Wallet.BalanceUsd kredisidir (bkz. RefundService.SubmitAsync).
/// </summary>
public class RefundServiceIntegrationTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly ManualTimeProvider _timeProvider;
    private readonly WalletService _walletService;
    private readonly RefundService _sut;

    public RefundServiceIntegrationTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var paymentProvider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance);
        _walletService = new WalletService(
            _db, new FixedPriceOracle(44.5m), paymentProvider, Options.Create(new PaymentConfig()), _timeProvider, NullLogger<WalletService>.Instance);

        _sut = new RefundService(_db, _walletService, _timeProvider, NullLogger<RefundService>.Instance);
    }

    private async Task<PaymentInvoice> SeedConfirmedInvoiceAsync()
    {
        var invoice = new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            MatchId = "match-1",
            PlayerId = "player-1",
            BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
            ReceivingAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4",
            Bip21Uri = "litecoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=0.02247696",
            AmountUsd = 1.00m,
            AmountLtc = 0.02247696m,
            LockedUsdPerLtc = 44.5m,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            RateServedFromCache = false,
            RateAgeSecondsAtUse = 0,
            Status = PaymentInvoiceStatus.Confirmed,
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            CreatedAt = _timeProvider.GetUtcNow(),
            ConfirmedAt = _timeProvider.GetUtcNow()
        };
        _db.PaymentInvoices.Add(invoice);
        await _db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task SubmitAsync_CalledTwiceForSameInvoice_OnlyOneRefundRow_AndOnlyOneCredit()
    {
        var invoice = await SeedConfirmedInvoiceAsync();

        await _sut.SubmitAsync(invoice, invoice.AmountUsd, RefundReason.MatchmakingTimeout, CancellationToken.None);
        await _db.SaveChangesAsync();
        await _sut.SubmitAsync(invoice, invoice.AmountUsd, RefundReason.MatchmakingTimeout, CancellationToken.None);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Refunds.CountAsync());
        Assert.Equal(invoice.AmountUsd, await _walletService.GetBalanceAsync("player-1", CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAndPersistAsync_CreditsPlayerWallet_AndFlipsConfirmedInvoiceToRefunded()
    {
        var invoice = await SeedConfirmedInvoiceAsync();

        await _sut.SubmitAndPersistAsync(invoice, invoice.AmountUsd, RefundReason.Manual, CancellationToken.None);

        var refund = await _db.Refunds.SingleAsync();
        Assert.Equal(invoice.AmountUsd, refund.AmountUsd);
        Assert.Equal(RefundReason.Manual, refund.Reason);
        Assert.Equal(invoice.AmountUsd, await _walletService.GetBalanceAsync("player-1", CancellationToken.None));

        var stored = await _db.PaymentInvoices.SingleAsync();
        Assert.Equal(PaymentInvoiceStatus.Refunded, stored.Status);
    }

    /// <summary>
    /// docs/05-payment.md Bölüm 10.1 admin "manuel iade" butonu çift tıklama/çift
    /// istek riski taşır — SubmitAndPersistAsync bu yüzden krediyi yalnızca Refund
    /// satırı DB seviyesinde (unique constraint) güvenle kalıcılaştıktan SONRA
    /// uygular. Burada aynı invoice/DbContext üzerinde art arda iki çağrı, gerçek
    /// bir eşzamanlı yarışı simüle eder (ikinci INSERT unique index'i ihlal eder).
    /// </summary>
    [Fact]
    public async Task SubmitAndPersistAsync_CalledTwiceForSameInvoice_OnlyOneRefundRow_AndOnlyOneCredit()
    {
        var invoice = await SeedConfirmedInvoiceAsync();

        await _sut.SubmitAndPersistAsync(invoice, invoice.AmountUsd, RefundReason.Manual, CancellationToken.None);
        await _sut.SubmitAndPersistAsync(invoice, invoice.AmountUsd, RefundReason.Manual, CancellationToken.None);

        Assert.Equal(1, await _db.Refunds.CountAsync());
        Assert.Equal(invoice.AmountUsd, await _walletService.GetBalanceAsync("player-1", CancellationToken.None));
    }

    [Fact]
    public async Task FindConfirmedInvoiceAsync_ConfirmedInvoiceExists_ReturnsIt()
    {
        var invoice = await SeedConfirmedInvoiceAsync();

        var found = await _sut.FindConfirmedInvoiceAsync("match-1", "player-1", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(invoice.Id, found!.Id);
    }

    [Fact]
    public async Task FindConfirmedInvoiceAsync_NoConfirmedInvoiceForPlayer_ReturnsNull()
    {
        await SeedConfirmedInvoiceAsync();

        var found = await _sut.FindConfirmedInvoiceAsync("match-1", "someone-else", CancellationToken.None);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindConfirmedInvoiceAsync_OnlyPendingInvoiceExists_ReturnsNull()
    {
        _db.PaymentInvoices.Add(new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            MatchId = "match-2",
            PlayerId = "player-1",
            BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
            ReceivingAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4",
            Bip21Uri = "litecoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=0.02247696",
            AmountUsd = 1.00m,
            AmountLtc = 0.02247696m,
            LockedUsdPerLtc = 44.5m,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            Status = PaymentInvoiceStatus.Pending, // henüz onaylanmadı — LeaveLobby bunun için refund tetiklememeli.
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            CreatedAt = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync();

        var found = await _sut.FindConfirmedInvoiceAsync("match-2", "player-1", CancellationToken.None);

        Assert.Null(found);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
