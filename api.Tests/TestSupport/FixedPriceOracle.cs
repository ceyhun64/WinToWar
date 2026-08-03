using api.Models.Payments;
using api.Services.Payments;

namespace api.Tests.TestSupport;

/// <summary>Testlerde deterministik bir kur döndüren sabit IPriceOracle.</summary>
public class FixedPriceOracle : IPriceOracle
{
    private readonly decimal _usdPerLtc;

    public FixedPriceOracle(decimal usdPerLtc)
    {
        _usdPerLtc = usdPerLtc;
    }

    public Task<PriceQuote> GetRateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new PriceQuote(_usdPerLtc, PriceOracleSource.CoinGecko, false, 0));
}
