using System.Security.Cryptography;
using System.Text;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 8.1: webhook imza doğrulaması. Header adı (WebhookSignatureHeader)
/// configurable'dır ama "sha256=" prefix'i (Bölüm 2.4) burada sabit kod olarak
/// tutulur — bir protokol detayı olduğu için config'e taşınmaz. Sabit zamanlı
/// karşılaştırma kullanılır (timing attack koruması).
/// </summary>
public static class WebhookSignatureValidator
{
    private const string SignaturePrefix = "sha256=";

    public static bool IsValid(string payload, string? signatureHeaderValue, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeaderValue) ||
            !signatureHeaderValue.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var providedHex = signatureHeaderValue[SignaturePrefix.Length..];
        var computedHex = ComputeHex(payload, secret);

        var providedBytes = Encoding.UTF8.GetBytes(providedHex.ToLowerInvariant());
        var computedBytes = Encoding.UTF8.GetBytes(computedHex);

        return providedBytes.Length == computedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes);
    }

    /// <summary>Test/simülasyon amaçlı: geçerli bir "sha256=..." header değeri üretir.</summary>
    public static string ComputeSignatureHeader(string payload, string secret) =>
        SignaturePrefix + ComputeHex(payload, secret);

    private static string ComputeHex(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
