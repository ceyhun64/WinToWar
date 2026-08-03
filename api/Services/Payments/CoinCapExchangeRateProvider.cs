using System.Globalization;
using System.Text.Json;
using api.Models.Payments;

namespace api.Services.Payments;

/// <summary>CoinCap assets endpoint üzerinden LTC/USD kuru (CoinGecko başarısız olursa fallback).</summary>
public class CoinCapExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;

    public CoinCapExchangeRateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public PriceOracleSource Source => PriceOracleSource.CoinCap;

    public async Task<decimal> GetUsdPerLtcAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync("v2/assets/litecoin", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var priceUsdText = document.RootElement.GetProperty("data").GetProperty("priceUsd").GetString()
            ?? throw new InvalidOperationException("CoinCap priceUsd alanı boş.");

        // 🔒 Bölüm 0.3: tüm decimal<->string dönüşümlerinde InvariantCulture zorunlu.
        var usd = decimal.Parse(priceUsdText, NumberStyles.Number, CultureInfo.InvariantCulture);
        if (usd <= 0)
        {
            throw new InvalidOperationException("CoinCap geçersiz kur döndürdü.");
        }

        return usd;
    }
}
