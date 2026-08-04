using api;
using api.Models.Payments;
using api.Services;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>Bölüm 0.3/11: retry gecikmeleri backoff + jitter içerir; retry limiti aşılınca Failed'e geçilir.</summary>
public class PayoutRetryTests : IDisposable
{
    private const decimal FixedRateUsdPerLtc = 40.0m;

    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly ManualTimeProvider _timeProvider;
    private readonly PaymentConfig _config;
    private readonly MatchManager _matchManager;
    private readonly PayoutService _sut;

    public PayoutRetryTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _config = new PaymentConfig { PayoutRetryCount = 2, PayoutRetryBaseDelaySeconds = 10, PayoutRetryJitterSeconds = 5 };

        var mapProvider = new MapProvider(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
        _matchManager = new MatchManager(mapProvider, TestEventLog.Writer(), NullLogger<MatchManager>.Instance);
        var failingProvider = new FailingPaymentProvider();
        var priceOracle = new FixedPriceOracle(FixedRateUsdPerLtc);
        var walletService = new WalletService(_db, priceOracle, failingProvider, Options.Create(_config), _timeProvider, NullLogger<WalletService>.Instance);

        _sut = new PayoutService(
            _db, failingProvider, priceOracle, walletService, _matchManager, Options.Create(_config), _timeProvider,
            new PaymentEventNotifier(new FakeHubContext()), NullLogger<PayoutService>.Instance);
    }

    private async Task<string> SeedMatchWithConfirmedInvoiceAsync()
    {
        var (match, _) = MatchFactory.CreateConfirmedVipMatch(_matchManager, 1.00m, ["winner"], _timeProvider.GetUtcNow().UtcDateTime);

        _db.PaymentInvoices.Add(new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            PlayerId = "winner",
            BtcPayInvoiceId = $"inv-{Guid.NewGuid():N}",
            AmountUsd = 1.00m,
            AmountLtc = 1.00m / FixedRateUsdPerLtc,
            LockedUsdPerLtc = FixedRateUsdPerLtc,
            PriceOracleSource = PriceOracleSource.CoinGecko,
            PayoutAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4",
            PayoutAddressFormat = PayoutAddressFormat.Bech32,
            Status = PaymentInvoiceStatus.Confirmed,
            ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(15),
            CreatedAt = _timeProvider.GetUtcNow(),
            ConfirmedAt = _timeProvider.GetUtcNow()
        });
        await _db.SaveChangesAsync();
        return match.Id;
    }

    [Fact]
    public async Task FailedSend_SchedulesRetryWithBackoffAndJitter_WithinExpectedWindow()
    {
        var matchId = await SeedMatchWithConfirmedInvoiceAsync();
        await _sut.ProcessPayoutAsync(matchId, ["winner"], CancellationToken.None);

        await _sut.ProcessDuePayoutsAsync(CancellationToken.None); // 1. deneme başarısız olur

        var recipient = await _db.PayoutRecipients.SingleAsync();
        Assert.Equal(PayoutStatus.PayoutPending, recipient.Status);
        Assert.Equal(1, recipient.RetryCount);
        Assert.NotNull(recipient.NextRetryAt);

        var minExpected = _timeProvider.GetUtcNow().AddSeconds(_config.PayoutRetryBaseDelaySeconds);
        var maxExpected = _timeProvider.GetUtcNow().AddSeconds(_config.PayoutRetryBaseDelaySeconds + _config.PayoutRetryJitterSeconds);
        Assert.InRange(recipient.NextRetryAt!.Value, minExpected, maxExpected);
    }

    [Fact]
    public async Task RetryLimitExceeded_MarksRecipientAndAggregateAsPermanentlyFailed()
    {
        var matchId = await SeedMatchWithConfirmedInvoiceAsync();
        await _sut.ProcessPayoutAsync(matchId, ["winner"], CancellationToken.None);

        // PayoutRetryCount=2: 3 deneme (ilk + 2 retry) sonunda kalıcı Failed olmalı.
        // Backoff üstel büyüdüğünden (10s, 20s, ...) her turdan sonra tam olarak
        // planlanan NextRetryAt'in ötesine ilerlenir (sabit bir artış yeterli olmaz).
        for (var i = 0; i <= _config.PayoutRetryCount; i++)
        {
            await _sut.ProcessDuePayoutsAsync(CancellationToken.None);
            var current = await _db.PayoutRecipients.AsNoTracking().SingleAsync();
            if (current.NextRetryAt is { } nextRetryAt)
            {
                _timeProvider.Advance(nextRetryAt - _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(1));
            }
        }

        var recipient = await _db.PayoutRecipients.SingleAsync();
        Assert.Equal(PayoutStatus.Failed, recipient.Status);
        Assert.Null(recipient.NextRetryAt);

        var payout = await _db.Payouts.SingleAsync();
        Assert.Equal(PayoutStatus.Failed, payout.Status);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
