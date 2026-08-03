namespace api.Services.Payments;

public interface IPriceOracle
{
    /// <summary>
    /// Bölüm 1.2 üç kademeli politikayı uygular: fresh cache → canlı sağlayıcılar
    /// (single-flight) → stale cache (+warning) → <see cref="PriceOracleUnavailableException"/>.
    /// </summary>
    Task<PriceQuote> GetRateAsync(CancellationToken cancellationToken);
}
