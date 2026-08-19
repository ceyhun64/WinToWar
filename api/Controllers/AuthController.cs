using System.Security.Claims;
using api.Models.Auth.Dtos;
using api.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace api.Controllers;

/// <summary>
/// docs/11-auth.md Bölüm 6: register/google/google-link/login/refresh/logout/
/// forgot-password/reset-password/verify-email/change-password/me.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "wintowar_refresh";

    // 🛠️ Route base [Route("api/auth")] ile birebir aynı olmalı — Path yalnızca
    // bir öneki, tarayıcı gerçek istek path'i bu önekle başlamıyorsa cookie'yi
    // hiç göndermez (önceki "/auth" değeri "/api/auth/refresh" isteğiyle
    // eşleşmediğinden refresh cookie'si asla geri gönderilmiyordu).
    private const string RefreshCookiePath = "/api/auth";

    private readonly AuthService _authService;
    private readonly AuthRateLimiter _rateLimiter;
    private readonly AuthConfig _config;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        AuthService authService,
        AuthRateLimiter rateLimiter,
        IOptions<AuthConfig> config,
        IWebHostEnvironment environment
    )
    {
        _authService = authService;
        _rateLimiter = rateLimiter;
        _config = config.Value;
        _environment = environment;
    }

    private string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private Guid CurrentPlayerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            !_rateLimiter.TryConsume(
                RateLimitedAction.Register,
                ClientIp,
                _config.RegisterRateLimitPerHour,
                TimeSpan.FromHours(1)
            )
        )
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                Error("RATE_LIMITED", "Çok fazla istek. Lütfen daha sonra tekrar deneyin.")
            );
        }

        var result = await _authService.RegisterAsync(request, cancellationToken);
        if (!result.Success)
        {
            return MapFailure<AuthResponseDto>(result.FailureReason);
        }

        SetRefreshCookie(result.Value!.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            !_rateLimiter.TryConsume(
                RateLimitedAction.Login,
                ClientIp,
                _config.LoginRateLimitPerMinute,
                TimeSpan.FromMinutes(1)
            )
        )
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                Error("RATE_LIMITED", "Çok fazla deneme. Lütfen daha sonra tekrar deneyin.")
            );
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        if (!result.Success)
        {
            return MapFailure<AuthResponseDto>(result.FailureReason);
        }

        SetRefreshCookie(result.Value!.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponseDto>> GoogleAuth(
        [FromBody] GoogleAuthRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _authService.GoogleAuthAsync(request, cancellationToken);
        if (!result.Success)
        {
            return MapFailure<AuthResponseDto>(result.FailureReason);
        }

        SetRefreshCookie(result.Value!.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    /// <summary>Bölüm 1.2/3.2: yalnızca geçerli bir oturumla (mevcut parola girişi sonrası) çağrılabilir.</summary>
    [Authorize]
    [HttpPost("google/link")]
    public async Task<ActionResult<PlayerAccountDto>> LinkGoogle(
        [FromBody] GoogleLinkRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _authService.LinkGoogleAsync(
            CurrentPlayerId,
            request,
            cancellationToken
        );
        return result.Success
            ? Ok(result.Value)
            : MapFailure<PlayerAccountDto>(result.FailureReason);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh(CancellationToken cancellationToken)
    {
        if (
            !Request.Cookies.TryGetValue(RefreshCookieName, out var rawRefreshToken)
            || string.IsNullOrEmpty(rawRefreshToken)
        )
        {
            return Unauthorized(Error("INVALID_TOKEN", "Oturum bulunamadı."));
        }

        var result = await _authService.RefreshAsync(rawRefreshToken, cancellationToken);
        if (!result.Success)
        {
            Response.Cookies.Delete(RefreshCookieName, RefreshCookieDeleteOptions);
            return MapFailure<AuthResponseDto>(result.FailureReason);
        }

        SetRefreshCookie(result.Value!.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (
            Request.Cookies.TryGetValue(RefreshCookieName, out var rawRefreshToken)
            && !string.IsNullOrEmpty(rawRefreshToken)
        )
        {
            await _authService.LogoutAsync(rawRefreshToken, cancellationToken);
        }

        Response.Cookies.Delete(RefreshCookieName, RefreshCookieDeleteOptions);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        if (
            !_rateLimiter.TryConsume(
                RateLimitedAction.ForgotPassword,
                ClientIp,
                _config.ForgotPasswordRateLimitPerHour,
                TimeSpan.FromHours(1)
            )
        )
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                Error("RATE_LIMITED", "Çok fazla istek. Lütfen daha sonra tekrar deneyin.")
            );
        }

        await _authService.ForgotPasswordAsync(request.Email, cancellationToken);
        return NoContent();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _authService.ResetPasswordAsync(
            request.Token,
            request.NewPassword,
            cancellationToken
        );
        return result.Success ? NoContent() : MapFailure<object>(result.FailureReason).Result!;
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _authService.VerifyEmailAsync(request.Token, cancellationToken);
        return result.Success ? NoContent() : MapFailure<object>(result.FailureReason).Result!;
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await _authService.ChangePasswordAsync(
            CurrentPlayerId,
            request,
            cancellationToken
        );
        return result.Success ? NoContent() : MapFailure<object>(result.FailureReason).Result!;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<PlayerAccountDto>> Me(CancellationToken cancellationToken)
    {
        var dto = await _authService.GetMeAsync(CurrentPlayerId, cancellationToken);
        return dto is null ? Unauthorized() : Ok(dto);
    }

    /// <summary>
    /// 🐞 Canlı ortam bulgusu: web (win-to-war.vercel.app) ile API (wintowar.onrender.com)
    /// FARKLI site'lardır. SameSite=Strict bir cookie'yi yalnızca aynı site'tan çıkan
    /// isteklere iliştirir; bu yüzden refresh cookie'si tarayıcıda duruyor ama
    /// POST /api/auth/refresh'e HİÇ gönderilmiyordu. Sonuç: her sayfa yüklemesinde oturum
    /// düşüyor, ardından gelen her korumalı istek (ör. odaya katılma) 401 alıyordu.
    /// Cross-site bir web/API ayrımında tek geçerli değer None'dır — ve tarayıcılar None
    /// için Secure zorunlu kılar, production zaten https olduğundan bu sağlanır.
    ///
    /// Dev'de API http üzerinden çalıştığı için None kullanılamaz (Secure olmayan bir
    /// SameSite=None cookie'si tarayıcı tarafından tümüyle reddedilir); orada
    /// localhost:3000 ile localhost:5019 zaten AYNI site (SameSite port'a bakmaz)
    /// olduğundan Lax sorunsuz çalışır.
    ///
    /// ⚠️ Güvenlik notu (docs/11-auth.md Bölüm 4'teki "SameSite=Strict" satırını günceller):
    /// None, Strict'in sağladığı CSRF korumasını kaldırır. Bu projede kabul edilebilir,
    /// çünkü cookie yalnızca Path=/api/auth altındaki refresh ucunda kullanılır ve o uç
    /// yan etki üretmez (yalnızca yeni bir access token verir); para taşıyan tüm uçlar
    /// cookie'ye değil Authorization: Bearer header'ına bakar ve bir header CSRF
    /// saldırısıyla taklit edilemez.
    /// </summary>
    private SameSiteMode RefreshCookieSameSite =>
        _environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;

    /// <summary>
    /// Cookie silme de aynı SameSite/Secure/Path üçlüsüyle yazılmalıdır: SameSite=None bir
    /// Set-Cookie başlığı Secure olmadan reddedilir, yani öznitelikler eşleşmezse çıkış
    /// yapıldığında cookie tarayıcıda silinmeden kalırdı.
    /// </summary>
    private CookieOptions RefreshCookieDeleteOptions =>
        new()
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = RefreshCookieSameSite,
            Path = RefreshCookiePath,
        };

    private void SetRefreshCookie(string rawRefreshToken)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            rawRefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                // 🛠️ Dev'de API http://localhost:5019 üzerinden çalışıyor — Secure=true
                // sabitken tarayıcı cookie'yi hiç saklamıyordu (Secure cookie'ler yalnızca
                // https üzerinden kabul edilir), bu da her sayfa yenilemesinde oturumun
                // sıfırlanmasına yol açıyordu. Production'da (https) Secure=true kalır.
                Secure = !_environment.IsDevelopment(),
                SameSite = RefreshCookieSameSite,
                Path = RefreshCookiePath,
                Expires = DateTimeOffset.UtcNow.AddDays(_config.RefreshTokenLifetimeDays),
            }
        );
    }

    private static AuthErrorResponse Error(string code, string message) =>
        new() { Code = code, Message = message };

    private ActionResult<T> MapFailure<T>(AuthFailureReason reason) =>
        reason switch
        {
            AuthFailureReason.InvalidCredentials => Unauthorized(
                Error("INVALID_CREDENTIALS", "E-posta veya şifre hatalı.")
            ),
            AuthFailureReason.AccountLocked => StatusCode(
                StatusCodes.Status423Locked,
                Error(
                    "ACCOUNT_LOCKED",
                    "Çok fazla başarısız deneme. Hesabınız geçici olarak kilitlendi."
                )
            ),
            AuthFailureReason.AccountDeleted => Unauthorized(
                Error("ACCOUNT_DELETED", "Bu hesap silinmiş.")
            ),
            AuthFailureReason.EmailAlreadyExists => Conflict(
                Error("EMAIL_ALREADY_EXISTS", "Bu e-posta ile zaten bir hesap var.")
            ),
            AuthFailureReason.GoogleAlreadyLinked => Conflict(
                Error("GOOGLE_ALREADY_LINKED", "Bu Google hesabı başka bir kullanıcıya bağlı.")
            ),
            AuthFailureReason.EmailExistsLinkRequired => Conflict(
                Error(
                    "EMAIL_EXISTS_LINK_REQUIRED",
                    "Bu e-posta ile zaten bir hesap var. Önce parolanızla giriş yapıp Google'ı bağlayın."
                )
            ),
            AuthFailureReason.GoogleIdentityInvalid => Unauthorized(
                Error("GOOGLE_IDENTITY_INVALID", "Google kimlik doğrulaması başarısız.")
            ),
            AuthFailureReason.InvalidToken => BadRequest(
                Error("INVALID_TOKEN", "Geçersiz veya kullanılmış token.")
            ),
            AuthFailureReason.TokenExpired => BadRequest(
                Error("TOKEN_EXPIRED", "Token süresi dolmuş.")
            ),
            AuthFailureReason.WeakPassword => BadRequest(
                Error("WEAK_PASSWORD", $"Şifre en az {_config.MinPasswordLength} karakter olmalı.")
            ),
            AuthFailureReason.MissingConsent => BadRequest(
                Error("MISSING_CONSENT", "Yaş ve şartlar onayı gereklidir.")
            ),
            AuthFailureReason.RefreshTokenReuseDetected => Unauthorized(
                Error(
                    "REFRESH_TOKEN_REUSE_DETECTED",
                    "Oturum güvenlik nedeniyle sonlandırıldı, tekrar giriş yapın."
                )
            ),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError,
                Error("UNKNOWN_ERROR", "Beklenmeyen bir hata oluştu.")
            ),
        };
}
