using api.Models.Payments;

namespace api.Services.Payments;

/// <summary>Bölüm 1.2 — IPriceOracle.GetRateAsync sonucu.</summary>
public record PriceQuote(
    decimal UsdPerLtc,
    PriceOracleSource Source,
    bool RateServedFromCache,
    int RateAgeSecondsAtUse);
