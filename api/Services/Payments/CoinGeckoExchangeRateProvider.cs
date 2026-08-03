using System.Text.Json;
using api.Models.Payments;

namespace api.Services.Payments;

/// <summary>CoinGecko simple/price endpoint üzerinden LTC/USD kuru.</summary>
public class CoinGeckoExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;

    public CoinGeckoExchangeRateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public PriceOracleSource Source => PriceOracleSource.CoinGecko;

    public async Task<decimal> GetUsdPerLtcAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            "api/v3/simple/price?ids=litecoin&vs_currencies=usd",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var usd = document.RootElement.GetProperty("litecoin").GetProperty("usd").GetDecimal();
        if (usd <= 0)
        {
            throw new InvalidOperationException("CoinGecko geçersiz kur döndürdü.");
        }

        return usd;
    }
}
