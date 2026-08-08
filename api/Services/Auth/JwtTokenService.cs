using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using api.Models.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace api.Services.Auth;

public record IssuedRefreshToken(string RawValue, string TokenHash, DateTime ExpiresAtUtc);

/// <summary>
/// docs/11-auth.md Bölüm 1.4: kısa ömürlü JWT access token (sub=PlayerId,
/// role=Player/Admin) + kriptografik olarak güvenli, DB'de yalnızca hash'i tutulan
/// rotating refresh token. Zaman burada da TimeProvider üzerinden alınır
/// (05-payment.md Bölüm 0.3 ile tutarlı disiplin — ödeme modülüne dokunulmaz ama
/// aynı prensip auth için de geçerli, test edilebilirlik).
/// </summary>
public class JwtTokenService
{
    private readonly AuthConfig _config;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<AuthConfig> config, TimeProvider timeProvider)
    {
        _config = config.Value;
        _timeProvider = timeProvider;
    }

    public (string AccessToken, DateTime ExpiresAtUtc) IssueAccessToken(PlayerAccount player)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_config.AccessTokenLifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new Claim(ClaimTypes.Role, player.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.JwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config.JwtIssuer,
            audience: _config.JwtAudience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public IssuedRefreshToken IssueRefreshToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawValue = Convert.ToBase64String(rawBytes);
        var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddDays(_config.RefreshTokenLifetimeDays);
        return new IssuedRefreshToken(rawValue, HashToken(rawValue), expiresAt);
    }

    /// <summary>Reset/verification token'ları da aynı hash-at-rest disiplinini paylaşır (Bölüm 2.2).</summary>
    public static string HashToken(string rawValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(bytes);
    }

    public static string GenerateOpaqueToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
