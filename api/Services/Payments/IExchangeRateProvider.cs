using api.Models.Payments;

namespace api.Services.Payments;

/// <summary>Tek bir canlı kur sağlayıcısı (CoinGecko/CoinCap). CompositePriceOracle bunları sırayla dener.</summary>
public interface IExchangeRateProvider
{
    PriceOracleSource Source { get; }

    Task<decimal> GetUsdPerLtcAsync(CancellationToken cancellationToken);
}
