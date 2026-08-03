namespace api.Services.Payments;

/// <summary>
/// Bölüm 1.2, kademe 3: cache stale-max aşıldı ve tüm canlı sağlayıcılar da
/// başarısız oldu. Bu durumda invoice oluşturulmaz.
/// </summary>
public class PriceOracleUnavailableException : Exception
{
    public PriceOracleUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
