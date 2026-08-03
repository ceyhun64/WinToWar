using api;
using api.Models.Payments;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// Bölüm 3.2 + 5.2 + 2.6: payout hesaplama, gönderim, ve yalnızca actual fee'nin
/// kalıcılaşması (reconciliation) uçtan uca — gerçek SQLite'a karşı.
/// </summary>
public class PayoutServiceIntegrationTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly PaymentConfig _config;
    private readonly ManualTimeProvider _timeProvider;
    private readonly FakePaymentProvider _paymentProvider;
    private readonly FakeHubContext _hubContext;
    private readonly PayoutService _sut;

    public PayoutServiceIntegrationTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _config = new PaymentConfig { CommissionRate = 0.10m };
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _paymentProvider = new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance);
        _hubContext = new FakeHubContext();

        _sut = new PayoutService(
            _db, _paymentProvider, Options.Create(_config), _timeProvider,
            new PaymentEventNotifier(_hubContext), NullLogger<PayoutService>.Instance);
    }

    private async Task SeedConfirmedInvoiceAsync(string matchId, string playerId, decimal amountLtc, string payoutAddress)
    {
        _db.PaymentInvoices.Add(new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            PlayerId = playerId,
            BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
            AmountUsd = 1.00m,
            AmountLtc = amountLtc,
            LockedUsdPerLtc = 44.5m,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            RateServedFromCache = false,
            RateAgeSecondsAtUse = 0,
            PayoutAddress = payoutAddress,
            PayoutAddressFormat = PayoutAddressFormat.Bech32,
            Status = PaymentInvoiceStatus.Confirmed,
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            CreatedAt = _timeProvider.GetUtcNow(),
            ConfirmedAt = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessPayoutAsync_ComputesPoolCommissionAndAmount_WithNullNetworkFee()
    {
        await SeedConfirmedInvoiceAsync("match-1", "winner", 0.02247696m, "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4");
        await SeedConfirmedInvoiceAsync("match-1", "loser", 0.02247696m, "bc1q0000000000000000000000000000000000000");

        await _sut.ProcessPayoutAsync("match-1", "winner", CancellationToken.None);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.PayoutPending, payout.Status);
        Assert.Null(payout.NetworkFeeLtc); // 🔒 Bölüm 2.6: tahmini fee asla persist edilmez.
        Assert.Equal("winner", payout.WinnerPlayerId);
        Assert.Equal(0.04495392m, payout.TotalPoolLtc);
        Assert.Equal(0.00449539m, payout.CommissionLtc);
    }

    [Fact]
    public async Task ProcessPayoutAsync_CalledTwice_IsIdempotent_OnlyOnePayoutRow()
    {
        await SeedConfirmedInvoiceAsync("match-1", "winner", 0.02247696m, "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4");

        await _sut.ProcessPayoutAsync("match-1", "winner", CancellationToken.None);
        await _sut.ProcessPayoutAsync("match-1", "winner", CancellationToken.None);

        Assert.Equal(1, await _db.Payouts.CountAsync());
    }

    [Fact]
    public async Task FullLifecycle_PendingToSentToCompleted_WithActualFeeOnly()
    {
        await SeedConfirmedInvoiceAsync("match-1", "winner", 0.02247696m, "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4");
        await _sut.ProcessPayoutAsync("match-1", "winner", CancellationToken.None);

        await _sut.ProcessDuePayoutsAsync(CancellationToken.None);
        var sent = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.PayoutSent, sent.Status);
        Assert.NotNull(sent.BtcPayTransactionId);
        Assert.Null(sent.NetworkFeeLtc);

        await _sut.ReconcileSentPayoutsAsync(CancellationToken.None);
        var completed = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.Completed, completed.Status);
        Assert.NotNull(completed.NetworkFeeLtc);
        Assert.NotNull(completed.CompletedAt);
        Assert.Single(_hubContext.Proxy.Sent);
        Assert.Equal("PayoutCompleted", _hubContext.Proxy.Sent[0].Method);
    }

    [Fact]
    public async Task ReconcileSentPayouts_DoesNotOverwriteAlreadyFilledNetworkFee()
    {
        await SeedConfirmedInvoiceAsync("match-1", "winner", 0.02247696m, "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4");
        await _sut.ProcessPayoutAsync("match-1", "winner", CancellationToken.None);
        await _sut.ProcessDuePayoutsAsync(CancellationToken.None);
        await _sut.ReconcileSentPayoutsAsync(CancellationToken.None);

        var firstFee = (await _db.Payouts.SingleAsync()).NetworkFeeLtc;

        // İkinci reconciliation turu: zaten Completed olduğundan bir daha işlenmemeli (kendi başına idempotency).
        await _sut.ReconcileSentPayoutsAsync(CancellationToken.None);
        var secondFee = (await _db.Payouts.SingleAsync()).NetworkFeeLtc;

        Assert.Equal(firstFee, secondFee);
        Assert.Single(_hubContext.Proxy.Sent); // yalnızca ilk turda notify edilmiş olmalı.
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
