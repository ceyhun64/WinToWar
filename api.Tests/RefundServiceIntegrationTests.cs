using api;
using api.Models.Payments;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>Bölüm 5.3 + 1.6/2.6: refund gönderimi ve reconciliation uçtan uca.</summary>
public class RefundServiceIntegrationTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly ManualTimeProvider _timeProvider;
    private readonly FakePaymentProvider _paymentProvider;
    private readonly FakeHubContext _hubContext;
    private readonly RefundService _sut;

    public RefundServiceIntegrationTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _paymentProvider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance);
        _hubContext = new FakeHubContext();

        _sut = new RefundService(
            _db, _paymentProvider, Options.Create(new PaymentConfig()), _timeProvider,
            new PaymentEventNotifier(_hubContext), NullLogger<RefundService>.Instance);
    }

    private async Task<PaymentInvoice> SeedConfirmedInvoiceAsync()
    {
        var invoice = new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            MatchId = "match-1",
            PlayerId = "player-1",
            BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
            AmountUsd = 1.00m,
            AmountLtc = 0.02247696m,
            LockedUsdPerLtc = 44.5m,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            RateServedFromCache = false,
            RateAgeSecondsAtUse = 0,
            PayoutAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4",
            PayoutAddressFormat = PayoutAddressFormat.Bech32,
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
    public async Task SubmitAsync_CalledTwiceForSameInvoice_OnlyOneRefundRow()
    {
        var invoice = await SeedConfirmedInvoiceAsync();

        await _sut.SubmitAsync(invoice, invoice.AmountLtc, RefundReason.MatchmakingTimeout, CancellationToken.None);
        await _db.SaveChangesAsync();
        await _sut.SubmitAsync(invoice, invoice.AmountLtc, RefundReason.MatchmakingTimeout, CancellationToken.None);
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.Refunds.CountAsync());
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
            AmountUsd = 1.00m,
            AmountLtc = 0.02247696m,
            LockedUsdPerLtc = 44.5m,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            PayoutAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4",
            PayoutAddressFormat = PayoutAddressFormat.Bech32,
            Status = PaymentInvoiceStatus.Pending, // henüz onaylanmadı — LeaveLobby bunun için refund tetiklememeli.
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            CreatedAt = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync();

        var found = await _sut.FindConfirmedInvoiceAsync("match-2", "player-1", CancellationToken.None);

        Assert.Null(found);
    }

    [Fact]
    public async Task FullLifecycle_PendingToSentToCompleted()
    {
        var invoice = await SeedConfirmedInvoiceAsync();
        await _sut.SubmitAsync(invoice, invoice.AmountLtc, RefundReason.MatchmakingTimeout, CancellationToken.None);
        await _db.SaveChangesAsync();

        var pending = await _db.Refunds.SingleAsync();
        Assert.Equal(RefundStatus.RefundPending, pending.Status);

        await _sut.ProcessDueRefundsAsync(CancellationToken.None);
        var sent = await _db.Refunds.SingleAsync();
        Assert.Equal(RefundStatus.RefundSent, sent.Status);
        Assert.NotNull(sent.BtcPayTransactionId);

        await _sut.ReconcileSentRefundsAsync(CancellationToken.None);
        var completed = await _db.Refunds.SingleAsync();
        Assert.Equal(RefundStatus.Completed, completed.Status);
        Assert.Single(_hubContext.Proxy.Sent);
        Assert.Equal("RefundCompleted", _hubContext.Proxy.Sent[0].Method);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
