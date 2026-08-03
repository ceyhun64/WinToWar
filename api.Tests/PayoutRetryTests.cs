using api;
using api.Models.Payments;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>Bölüm 0.3/11: retry gecikmeleri backoff + jitter içerir; retry limiti aşılınca Failed'e geçilir.</summary>
public class PayoutRetryTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly ManualTimeProvider _timeProvider;
    private readonly PaymentConfig _config;
    private readonly PayoutService _sut;

    public PayoutRetryTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _config = new PaymentConfig { PayoutRetryCount = 2, PayoutRetryBaseDelaySeconds = 10, PayoutRetryJitterSeconds = 5 };

        _sut = new PayoutService(
            _db, new FailingPaymentProvider(), Options.Create(_config), _timeProvider,
            new PaymentEventNotifier(new FakeHubContext()), NullLogger<PayoutService>.Instance);
    }

    private async Task SeedConfirmedInvoiceAsync()
    {
        _db.PaymentInvoices.Add(new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            MatchId = "match-1",
            PlayerId = "winner",
            BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
            AmountUsd = 1.00m,
            AmountLtc = 0.02247696m,
            LockedUsdPerLtc = 44.5m,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            PayoutAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4",
            PayoutAddressFormat = PayoutAddressFormat.Bech32,
            Status = PaymentInvoiceStatus.Confirmed,
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            CreatedAt = _timeProvider.GetUtcNow(),
            ConfirmedAt = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task FailedSend_SchedulesRetryWithBackoffAndJitter_WithinExpectedWindow()
    {
        await SeedConfirmedInvoiceAsync();
        await _sut.ProcessPayoutAsync("match-1", "winner", CancellationToken.None);

        await _sut.ProcessDuePayoutsAsync(CancellationToken.None); // 1. deneme başarısız olur

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.PayoutPending, payout.Status);
        Assert.Equal(1, payout.RetryCount);
        Assert.NotNull(payout.NextRetryAt);

        var minExpected = _timeProvider.GetUtcNow().AddSeconds(_config.PayoutRetryBaseDelaySeconds);
        var maxExpected = _timeProvider.GetUtcNow().AddSeconds(_config.PayoutRetryBaseDelaySeconds + _config.PayoutRetryJitterSeconds);
        Assert.InRange(payout.NextRetryAt!.Value, minExpected, maxExpected);
    }

    [Fact]
    public async Task RetryLimitExceeded_MarksPayoutAsPermanentlyFailed()
    {
        await SeedConfirmedInvoiceAsync();
        await _sut.ProcessPayoutAsync("match-1", "winner", CancellationToken.None);

        // PayoutRetryCount=2: 3 deneme (ilk + 2 retry) sonunda kalıcı Failed olmalı.
        // Backoff üstel büyüdüğünden (10s, 20s, ...) her turdan sonra tam olarak
        // planlanan NextRetryAt'in ötesine ilerlenir (sabit bir artış yeterli olmaz).
        for (var i = 0; i <= _config.PayoutRetryCount; i++)
        {
            await _sut.ProcessDuePayoutsAsync(CancellationToken.None);
            var current = await _db.Payouts.AsNoTracking().SingleAsync();
            if (current.NextRetryAt is { } nextRetryAt)
            {
                _timeProvider.Advance(nextRetryAt - _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(1));
            }
        }

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.Failed, payout.Status);
        Assert.Null(payout.NextRetryAt);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
