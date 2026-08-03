using api;
using api.Models.Payments;
using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>Bölüm 1.2: fresh/stale cache politikası, single-flight ve fallback zinciri.</summary>
public class CompositePriceOracleTests
{
    private static PaymentConfig DefaultConfig => new()
    {
        PriceCacheFreshSeconds = 30,
        PriceCacheStaleMaxSeconds = 300,
        PriceOracleTimeoutSeconds = 5
    };

    private static CompositePriceOracle CreateSut(
        IEnumerable<FakeExchangeRateProvider> providers, PaymentConfig config, ManualTimeProvider timeProvider) =>
        new(providers, Options.Create(config), timeProvider, NullLogger<CompositePriceOracle>.Instance);

    [Fact]
    public async Task FreshCache_IsUsedDirectly_WithoutCallingProvider()
    {
        var coinGecko = new FakeExchangeRateProvider(PriceOracleSource.CoinGecko) { ResultFactory = () => 42m };
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var sut = CreateSut([coinGecko], DefaultConfig, timeProvider);

        var first = await sut.GetRateAsync(CancellationToken.None);
        timeProvider.Advance(TimeSpan.FromSeconds(10)); // hâlâ fresh pencere içinde (30sn)
        var second = await sut.GetRateAsync(CancellationToken.None);

        Assert.Equal(1, coinGecko.CallCount);
        Assert.False(first.RateServedFromCache);
        Assert.True(second.RateServedFromCache);
        Assert.Equal(PriceOracleSource.CoinGecko, second.Source); // cache'ten dönse de gerçek provider adı korunur.
    }

    [Fact]
    public async Task ConcurrentCacheMiss_TriggersOnlyOneExternalCall_SingleFlight()
    {
        var coinGecko = new FakeExchangeRateProvider(PriceOracleSource.CoinGecko)
        {
            ResultFactory = () => 44.5m,
            Delay = TimeSpan.FromMilliseconds(150)
        };
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var sut = CreateSut([coinGecko], DefaultConfig, timeProvider);

        var tasks = Enumerable.Range(0, 50).Select(_ => sut.GetRateAsync(CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, coinGecko.CallCount);
        Assert.All(results, r => Assert.Equal(44.5m, r.UsdPerLtc));
    }

    [Fact]
    public async Task StaleCache_UsedWhenLiveProvidersFail_WithinStaleMaxWindow()
    {
        var coinGecko = new FakeExchangeRateProvider(PriceOracleSource.CoinGecko) { ResultFactory = () => 44.5m };
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var config = DefaultConfig;
        var sut = CreateSut([coinGecko], config, timeProvider);

        var fresh = await sut.GetRateAsync(CancellationToken.None);

        // Fresh pencereyi aş (30sn) ama stale-max'i aşma (300sn); şimdi canlı sağlayıcı başarısız olsun.
        timeProvider.Advance(TimeSpan.FromSeconds(100));
        coinGecko.ThrowException = new HttpRequestException("boom");

        var stale = await sut.GetRateAsync(CancellationToken.None);

        Assert.True(stale.RateServedFromCache);
        Assert.Equal(fresh.UsdPerLtc, stale.UsdPerLtc);
        Assert.Equal(PriceOracleSource.CoinGecko, stale.Source);
        Assert.True(stale.RateAgeSecondsAtUse >= 100);
    }

    [Fact]
    public async Task NoCache_AllProvidersFail_ThrowsPriceOracleUnavailable()
    {
        var coinGecko = new FakeExchangeRateProvider(PriceOracleSource.CoinGecko) { ThrowException = new HttpRequestException("boom") };
        var coinCap = new FakeExchangeRateProvider(PriceOracleSource.CoinCap) { ThrowException = new HttpRequestException("boom") };
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var sut = CreateSut([coinGecko, coinCap], DefaultConfig, timeProvider);

        await Assert.ThrowsAsync<PriceOracleUnavailableException>(() => sut.GetRateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StaleMaxExceeded_AndProvidersFail_ThrowsPriceOracleUnavailable()
    {
        var coinGecko = new FakeExchangeRateProvider(PriceOracleSource.CoinGecko) { ResultFactory = () => 44.5m };
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var config = DefaultConfig;
        var sut = CreateSut([coinGecko], config, timeProvider);

        await sut.GetRateAsync(CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromSeconds(config.PriceCacheStaleMaxSeconds + 1));
        coinGecko.ThrowException = new HttpRequestException("boom");

        await Assert.ThrowsAsync<PriceOracleUnavailableException>(() => sut.GetRateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FirstProviderFails_FallsBackToSecondProvider()
    {
        var coinGecko = new FakeExchangeRateProvider(PriceOracleSource.CoinGecko) { ThrowException = new HttpRequestException("boom") };
        var coinCap = new FakeExchangeRateProvider(PriceOracleSource.CoinCap) { ResultFactory = () => 43.9m };
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var sut = CreateSut([coinGecko, coinCap], DefaultConfig, timeProvider);

        var result = await sut.GetRateAsync(CancellationToken.None);

        Assert.Equal(PriceOracleSource.CoinCap, result.Source);
        Assert.Equal(43.9m, result.UsdPerLtc);
    }
}
