using api.Models.Auth;
using api.Models.Auth.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Auth;

public enum AuthFailureReason
{
    InvalidCredentials,
    AccountLocked,
    AccountDeleted,
    EmailAlreadyExists,
    GoogleAlreadyLinked,
    EmailExistsLinkRequired,
    GoogleIdentityInvalid,
    InvalidToken,
    TokenExpired,
    WeakPassword,
    MissingConsent,
    RefreshTokenReuseDetected,
}

public class AuthOperationResult<T>
{
    public bool Success { get; private init; }
    public AuthFailureReason FailureReason { get; private init; }
    public T? Value { get; private init; }

    public static AuthOperationResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static AuthOperationResult<T> Fail(AuthFailureReason reason) => new() { Success = false, FailureReason = reason };
}

public record AuthSession(AuthResponseDto Response, string RawRefreshToken);

/// <summary>
/// docs/11-auth.md — register/login/google/refresh/logout/forgot/reset/change-password/
/// verify-email/me. Beklenen hata durumları (06-coding-standards.md "Exception ve Guard")
/// exception yerine <see cref="AuthOperationResult{T}"/> ile döndürülür; controller bunu
/// HTTP status/koduna map'ler.
/// </summary>
public class AuthService
{
    private readonly AuthDbContext _db;
    private readonly JwtTokenService _tokenService;
    private readonly IGoogleIdTokenValidator _googleValidator;
    private readonly IEmailSender _emailSender;
    private readonly PasswordHasher<PlayerAccount> _passwordHasher = new();
    private readonly AuthConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AuthDbContext db,
        JwtTokenService tokenService,
        IGoogleIdTokenValidator googleValidator,
        IEmailSender emailSender,
        IOptions<AuthConfig> config,
        TimeProvider timeProvider,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _googleValidator = googleValidator;
        _emailSender = emailSender;
        _config = config.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<AuthOperationResult<AuthSession>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.Password.Length < _config.MinPasswordLength)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.WeakPassword);
        }

        if (!request.AgeConfirmed || !request.TermsAccepted)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.MissingConsent);
        }

        var email = NormalizeEmail(request.Email);
        if (await _db.PlayerAccounts.AnyAsync(p => p.Email == email, cancellationToken))
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.EmailAlreadyExists);
        }

        var player = new PlayerAccount
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active,
            AgeConfirmedAt = Now,
            TermsAcceptedAt = Now,
            CreatedAt = Now,
        };
        player.PasswordHash = _passwordHasher.HashPassword(player, request.Password);

        _db.PlayerAccounts.Add(player);
        await SendEmailVerificationAsync(player, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<AuthSession>.Ok(await IssueSessionAsync(player, cancellationToken));
    }

    public async Task<AuthOperationResult<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var player = await _db.PlayerAccounts.SingleOrDefaultAsync(p => p.Email == email, cancellationToken);

        if (player is null || player.PasswordHash is null)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.InvalidCredentials);
        }

        if (player.Status == PlayerStatus.Deleted)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.AccountDeleted);
        }

        if (player.LockedUntil is not null && player.LockedUntil > Now)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.AccountLocked);
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(player, player.PasswordHash, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            player.FailedLoginAttempts += 1;
            if (player.FailedLoginAttempts >= _config.MaxFailedLoginAttempts)
            {
                player.LockedUntil = Now.AddMinutes(_config.LockoutDurationMinutes);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return player.LockedUntil is not null && player.LockedUntil > Now
                ? AuthOperationResult<AuthSession>.Fail(AuthFailureReason.AccountLocked)
                : AuthOperationResult<AuthSession>.Fail(AuthFailureReason.InvalidCredentials);
        }

        player.FailedLoginAttempts = 0;
        player.LockedUntil = null;
        player.LastLoginAt = Now;
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<AuthSession>.Ok(await IssueSessionAsync(player, cancellationToken));
    }

    /// <summary>
    /// docs/11-auth.md Bölüm 1.2/3.2 (v3): email çakışmasında OTOMATIK BAĞLAMA YOK —
    /// 409 EMAIL_EXISTS_LINK_REQUIRED döner, hesap oluşturulmaz/bağlanmaz.
    /// </summary>
    public async Task<AuthOperationResult<AuthSession>> GoogleAuthAsync(GoogleAuthRequest request, CancellationToken cancellationToken)
    {
        var identity = await _googleValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.GoogleIdentityInvalid);
        }

        var existingByGoogleId = await _db.PlayerAccounts.SingleOrDefaultAsync(p => p.GoogleId == identity.GoogleId, cancellationToken);
        if (existingByGoogleId is not null)
        {
            if (existingByGoogleId.Status == PlayerStatus.Deleted)
            {
                return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.AccountDeleted);
            }

            existingByGoogleId.LastLoginAt = Now;
            await _db.SaveChangesAsync(cancellationToken);
            return AuthOperationResult<AuthSession>.Ok(await IssueSessionAsync(existingByGoogleId, cancellationToken));
        }

        var email = NormalizeEmail(identity.Email);
        var existingByEmail = await _db.PlayerAccounts.SingleOrDefaultAsync(p => p.Email == email, cancellationToken);
        if (existingByEmail is not null)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.EmailExistsLinkRequired);
        }

        if (!request.AgeConfirmed || !request.TermsAccepted)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.MissingConsent);
        }

        var player = new PlayerAccount
        {
            Email = email,
            GoogleId = identity.GoogleId,
            DisplayName = identity.DisplayName,
            Role = PlayerRole.Player,
            Status = PlayerStatus.Active,
            EmailVerifiedAt = Now,
            AgeConfirmedAt = Now,
            TermsAcceptedAt = Now,
            CreatedAt = Now,
            LastLoginAt = Now,
        };

        _db.PlayerAccounts.Add(player);
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<AuthSession>.Ok(await IssueSessionAsync(player, cancellationToken));
    }

    /// <summary>Yalnızca geçerli bir oturumla çağrılabilir — playerId controller'da JWT'den okunur.</summary>
    public async Task<AuthOperationResult<PlayerAccountDto>> LinkGoogleAsync(Guid playerId, GoogleLinkRequest request, CancellationToken cancellationToken)
    {
        var identity = await _googleValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (identity is null)
        {
            return AuthOperationResult<PlayerAccountDto>.Fail(AuthFailureReason.GoogleIdentityInvalid);
        }

        if (await _db.PlayerAccounts.AnyAsync(p => p.GoogleId == identity.GoogleId, cancellationToken))
        {
            return AuthOperationResult<PlayerAccountDto>.Fail(AuthFailureReason.GoogleAlreadyLinked);
        }

        var player = await _db.PlayerAccounts.SingleAsync(p => p.Id == playerId, cancellationToken);
        player.GoogleId = identity.GoogleId;
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<PlayerAccountDto>.Ok(ToDto(player));
    }

    /// <summary>Bölüm 1.4: her refresh'te eski token iptal edilir, çalıntı token tekrar kullanılırsa tüm aktif token'lar iptal edilir.</summary>
    public async Task<AuthOperationResult<AuthSession>> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashToken(rawRefreshToken);
        var token = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.InvalidToken);
        }

        if (token.RevokedAt is not null)
        {
            if (_config.RevokeAllOnReuseDetected)
            {
                var activeTokens = await _db.RefreshTokens
                    .Where(t => t.PlayerId == token.PlayerId && t.RevokedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var active in activeTokens)
                {
                    active.RevokedAt = Now;
                }

                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Çalıntı refresh token tekrar kullanıldı, tüm aktif token'lar iptal edildi: {PlayerId}", token.PlayerId);
            }

            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.RefreshTokenReuseDetected);
        }

        if (token.ExpiresAt < Now)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.TokenExpired);
        }

        var player = await _db.PlayerAccounts.SingleOrDefaultAsync(p => p.Id == token.PlayerId, cancellationToken);
        if (player is null || player.Status == PlayerStatus.Deleted)
        {
            return AuthOperationResult<AuthSession>.Fail(AuthFailureReason.AccountDeleted);
        }

        token.RevokedAt = Now;
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<AuthSession>.Ok(await IssueSessionAsync(player, cancellationToken));
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashToken(rawRefreshToken);
        var token = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = Now;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Hesap enumeration'ı önlemek için her zaman aynı (başarılı) sonucu döner.</summary>
    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = NormalizeEmail(email);
        var player = await _db.PlayerAccounts.SingleOrDefaultAsync(p => p.Email == normalized, cancellationToken);
        if (player is null || player.Status == PlayerStatus.Deleted)
        {
            return;
        }

        var rawToken = JwtTokenService.GenerateOpaqueToken();
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            PlayerId = player.Id,
            TokenHash = JwtTokenService.HashToken(rawToken),
            ExpiresAt = Now.AddSeconds(_config.PasswordResetTokenExpirySeconds),
            CreatedAt = Now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        await _emailSender.SendAsync(
            player.Email,
            "WinToWar — Şifre Sıfırlama",
            $"Şifrenizi sıfırlamak için: /sifre-sifirla/{rawToken}",
            cancellationToken);
    }

    public async Task<AuthOperationResult<bool>> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken cancellationToken)
    {
        if (newPassword.Length < _config.MinPasswordLength)
        {
            return AuthOperationResult<bool>.Fail(AuthFailureReason.WeakPassword);
        }

        var hash = JwtTokenService.HashToken(rawToken);
        var token = await _db.PasswordResetTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || token.UsedAt is not null)
        {
            return AuthOperationResult<bool>.Fail(AuthFailureReason.InvalidToken);
        }

        if (token.ExpiresAt < Now)
        {
            return AuthOperationResult<bool>.Fail(AuthFailureReason.TokenExpired);
        }

        var player = await _db.PlayerAccounts.SingleAsync(p => p.Id == token.PlayerId, cancellationToken);
        player.PasswordHash = _passwordHasher.HashPassword(player, newPassword);
        player.FailedLoginAttempts = 0;
        player.LockedUntil = null;
        token.UsedAt = Now;

        await RevokeAllRefreshTokensAsync(player.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<bool>.Ok(true);
    }

    /// <summary>Google-only hesapta (PasswordHash null) CurrentPassword gerekmez — ilk parola bu şekilde belirlenir.</summary>
    public async Task<AuthOperationResult<bool>> ChangePasswordAsync(Guid playerId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.NewPassword.Length < _config.MinPasswordLength)
        {
            return AuthOperationResult<bool>.Fail(AuthFailureReason.WeakPassword);
        }

        var player = await _db.PlayerAccounts.SingleAsync(p => p.Id == playerId, cancellationToken);

        if (player.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) ||
                _passwordHasher.VerifyHashedPassword(player, player.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                return AuthOperationResult<bool>.Fail(AuthFailureReason.InvalidCredentials);
            }
        }

        player.PasswordHash = _passwordHasher.HashPassword(player, request.NewPassword);
        await RevokeAllRefreshTokensAsync(player.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<bool>.Ok(true);
    }

    public async Task<AuthOperationResult<bool>> VerifyEmailAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = JwtTokenService.HashToken(rawToken);
        var token = await _db.EmailVerificationTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || token.UsedAt is not null)
        {
            return AuthOperationResult<bool>.Fail(AuthFailureReason.InvalidToken);
        }

        if (token.ExpiresAt < Now)
        {
            return AuthOperationResult<bool>.Fail(AuthFailureReason.TokenExpired);
        }

        var player = await _db.PlayerAccounts.SingleAsync(p => p.Id == token.PlayerId, cancellationToken);
        player.EmailVerifiedAt = Now;
        token.UsedAt = Now;
        await _db.SaveChangesAsync(cancellationToken);

        return AuthOperationResult<bool>.Ok(true);
    }

    public async Task<PlayerAccountDto?> GetMeAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var player = await _db.PlayerAccounts.SingleOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        return player is null ? null : ToDto(player);
    }

    private async Task SendEmailVerificationAsync(PlayerAccount player, CancellationToken cancellationToken)
    {
        var rawToken = JwtTokenService.GenerateOpaqueToken();
        _db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            PlayerId = player.Id,
            TokenHash = JwtTokenService.HashToken(rawToken),
            ExpiresAt = Now.AddSeconds(_config.EmailVerificationTokenExpirySeconds),
            CreatedAt = Now,
        });

        await _emailSender.SendAsync(
            player.Email,
            "WinToWar — E-posta Doğrulama",
            $"E-postanızı doğrulamak için: /dogrula/{rawToken}",
            cancellationToken);
    }

    private async Task RevokeAllRefreshTokensAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.PlayerId == playerId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = Now;
        }
    }

    private async Task<AuthSession> IssueSessionAsync(PlayerAccount player, CancellationToken cancellationToken)
    {
        var (accessToken, expiresAt) = _tokenService.IssueAccessToken(player);
        var refreshToken = _tokenService.IssueRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            PlayerId = player.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAtUtc,
            CreatedAt = Now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        var response = new AuthResponseDto
        {
            AccessToken = accessToken,
            AccessTokenExpiresAtUtc = expiresAt,
            Player = ToDto(player),
        };

        return new AuthSession(response, refreshToken.RawValue);
    }

    private static PlayerAccountDto ToDto(PlayerAccount player) => new()
    {
        Id = player.Id.ToString(),
        Email = player.Email,
        DisplayName = player.DisplayName,
        Role = player.Role.ToString(),
        Status = player.Status.ToString(),
        EmailVerified = player.EmailVerifiedAt is not null,
        HasPassword = player.PasswordHash is not null,
        GoogleLinked = player.GoogleId is not null,
    };
}
