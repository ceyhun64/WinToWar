using api.Models.Payments;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 1.2: fresh cache → canlı sağlayıcılar (CoinGecko→CoinCap, single-flight
/// ile) → stale cache (+warning) → PRICE_ORACLE_UNAVAILABLE üç kademeli politikayı
/// uygulayan tek IPriceOracle implementasyonu.
/// </summary>
public class CompositePriceOracle : IPriceOracle
{
    private sealed record CachedRate(decimal UsdPerLtc, PriceOracleSource Source, DateTimeOffset FetchedAt);

    private readonly IReadOnlyList<IExchangeRateProvider> _providers;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompositePriceOracle> _logger;

    private CachedRate? _cache;
    private readonly object _singleFlightGate = new();
    private Task<CachedRate>? _inFlightFetch;

    public CompositePriceOracle(
        IEnumerable<IExchangeRateProvider> providers,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        ILogger<CompositePriceOracle> logger)
    {
        _providers = providers.ToList();
        _config = config.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PriceQuote> GetRateAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var cached = Volatile.Read(ref _cache);

        if (cached is not null)
        {
            var freshAge = (now - cached.FetchedAt).TotalSeconds;
            if (freshAge <= _config.PriceCacheFreshSeconds)
            {
                return new PriceQuote(cached.UsdPerLtc, cached.Source, true, (int)freshAge);
            }
        }

        try
        {
            var fetched = await FetchLiveSingleFlightAsync(cancellationToken);
            Volatile.Write(ref _cache, fetched);
            return new PriceQuote(fetched.UsdPerLtc, fetched.Source, false, 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (cached is not null)
            {
                var staleAge = (_timeProvider.GetUtcNow() - cached.FetchedAt).TotalSeconds;
                if (staleAge <= _config.PriceCacheStaleMaxSeconds)
                {
                    _logger.LogWarning(ex,
                        "Canlı kur sağlayıcıları başarısız oldu, stale cache kullanılıyor (age={AgeSeconds}s, source={Source}).",
                        (int)staleAge, cached.Source);
                    return new PriceQuote(cached.UsdPerLtc, cached.Source, true, (int)staleAge);
                }
            }

            throw new PriceOracleUnavailableException("PRICE_ORACLE_UNAVAILABLE", ex);
        }
    }

    /// <summary>
    /// Bölüm 1.2 single-flight: aynı cache miss penceresinde eşzamanlı gelen
    /// istekler dış API'ye ayrı ayrı gitmez; tek bir kilitli-görev (locked-task)
    /// paylaşılır. Paylaşılan fetch, çağıranların kendi CancellationToken'larından
    /// bağımsız çalışır (birinin iptali diğerlerini etkilemesin diye); her çağıran
    /// yalnızca kendi bekleyişini <c>WaitAsync</c> ile iptal edebilir.
    /// </summary>
    private async Task<CachedRate> FetchLiveSingleFlightAsync(CancellationToken cancellationToken)
    {
        Task<CachedRate> task;
        lock (_singleFlightGate)
        {
            if (_inFlightFetch is { IsCompleted: false })
            {
                task = _inFlightFetch;
            }
            else
            {
                task = FetchLiveWithFallbackAsync();
                _inFlightFetch = task;
            }
        }

        return await task.WaitAsync(cancellationToken);
    }

    private async Task<CachedRate> FetchLiveWithFallbackAsync()
    {
        Exception? lastError = null;
        foreach (var provider in _providers)
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.PriceOracleTimeoutSeconds));
            try
            {
                var rate = await provider.GetUsdPerLtcAsync(timeoutCts.Token);
                return new CachedRate(rate, provider.Source, _timeProvider.GetUtcNow());
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Kur sağlayıcısı başarısız: {Source}", provider.Source);
            }
        }

        throw new PriceOracleUnavailableException("Tüm canlı kur sağlayıcıları başarısız oldu.", lastError);
    }
}
