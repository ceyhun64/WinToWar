using api.Models.Auth;
using api.Models.Auth.Dtos;
using api.Services.Auth;
using api.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>
/// docs/11-auth.md Bölüm 8: register/login/lockout/refresh rotation/google
/// akışları/reset/change-password/verify-email — servis katmanında, gerçek bir
/// SQLite veritabanına (in-memory) karşı (bkz. PaymentServiceIntegrationTests
/// ile aynı desen).
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly AuthDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly AuthConfig _config;
    private readonly ManualTimeProvider _timeProvider;
    private readonly FakeGoogleIdTokenValidator _googleValidator;
    private readonly RecordingEmailSender _emailSender;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        (_db, _connection) = AuthDbContextFactory.CreateOpen();
        _config = new AuthConfig
        {
            MinPasswordLength = 8,
            MaxFailedLoginAttempts = 3,
            LockoutDurationMinutes = 15,
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 30,
            PasswordResetTokenExpirySeconds = 900,
            JwtSigningKey = "test-signing-key-at-least-32-bytes-long!!",
            JwtIssuer = "TestIssuer",
            JwtAudience = "TestAudience",
        };
        _timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        _googleValidator = new FakeGoogleIdTokenValidator();
        _emailSender = new RecordingEmailSender();

        var tokenService = new JwtTokenService(Options.Create(_config), _timeProvider);
        _sut = new AuthService(
            _db, tokenService, _googleValidator, _emailSender,
            Options.Create(_config), _timeProvider, NullLogger<AuthService>.Instance);
    }

    private static RegisterRequest ValidRegisterRequest(string email = "a@test.com") => new()
    {
        Email = email,
        Password = "SuperSecret1",
        DisplayName = "Alice",
        AgeConfirmed = true,
        TermsAccepted = true,
    };

    [Fact]
    public async Task Register_ValidInput_CreatesActiveAccount_AndIssuesSession()
    {
        var result = await _sut.RegisterAsync(ValidRegisterRequest(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Player", result.Value!.Response.Player.Role);
        Assert.Equal("Active", result.Value.Response.Player.Status);
        Assert.False(result.Value.Response.Player.EmailVerified);
        Assert.NotEmpty(result.Value.Response.AccessToken);
        Assert.NotEmpty(result.Value.RawRefreshToken);
        Assert.Single(_emailSender.SentEmails);
    }

    [Fact]
    public async Task Register_WeakPassword_Rejected()
    {
        var weak = new RegisterRequest { Email = "b@test.com", Password = "short", DisplayName = "Bob", AgeConfirmed = true, TermsAccepted = true };

        var result = await _sut.RegisterAsync(weak, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.WeakPassword, result.FailureReason);
    }

    [Fact]
    public async Task Register_MissingConsent_Rejected()
    {
        var request = new RegisterRequest { Email = "c@test.com", Password = "SuperSecret1", DisplayName = "Cem", AgeConfirmed = false, TermsAccepted = true };

        var result = await _sut.RegisterAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.MissingConsent, result.FailureReason);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Rejected()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("dup@test.com"), CancellationToken.None);

        var result = await _sut.RegisterAsync(ValidRegisterRequest("dup@test.com"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.EmailAlreadyExists, result.FailureReason);
    }

    [Fact]
    public async Task Login_CorrectPassword_Succeeds()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("login@test.com"), CancellationToken.None);

        var result = await _sut.LoginAsync(new LoginRequest { Email = "login@test.com", Password = "SuperSecret1" }, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Login_WrongPassword_ReachingMaxAttempts_LocksAccount()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("lock@test.com"), CancellationToken.None);
        var badLogin = new LoginRequest { Email = "lock@test.com", Password = "WrongPassword1" };

        AuthOperationResult<AuthSession>? last = null;
        for (var i = 0; i < _config.MaxFailedLoginAttempts; i++)
        {
            last = await _sut.LoginAsync(badLogin, CancellationToken.None);
        }

        Assert.False(last!.Success);
        Assert.Equal(AuthFailureReason.AccountLocked, last.FailureReason);

        // Doğru şifreyle bile kilit süresi dolmadan giriş yapılamaz.
        var correctAttempt = await _sut.LoginAsync(
            new LoginRequest { Email = "lock@test.com", Password = "SuperSecret1" }, CancellationToken.None);
        Assert.False(correctAttempt.Success);
        Assert.Equal(AuthFailureReason.AccountLocked, correctAttempt.FailureReason);

        // Kilit süresi dolunca tekrar deneme hakkı doğar.
        _timeProvider.Advance(TimeSpan.FromMinutes(_config.LockoutDurationMinutes + 1));
        var afterLockout = await _sut.LoginAsync(
            new LoginRequest { Email = "lock@test.com", Password = "SuperSecret1" }, CancellationToken.None);
        Assert.True(afterLockout.Success);
    }

    [Fact]
    public async Task Login_DeletedAccount_Returns401()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest("deleted@test.com"), CancellationToken.None);
        var player = _db.PlayerAccounts.Single(p => p.Email == "deleted@test.com");
        player.Status = PlayerStatus.Deleted;
        await _db.SaveChangesAsync();

        var result = await _sut.LoginAsync(new LoginRequest { Email = "deleted@test.com", Password = "SuperSecret1" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.AccountDeleted, result.FailureReason);
    }

    [Fact]
    public async Task Login_SuspendedAccount_StillSucceeds_ButStatusReflectsSuspended()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("suspended@test.com"), CancellationToken.None);
        var player = _db.PlayerAccounts.Single(p => p.Email == "suspended@test.com");
        player.Status = PlayerStatus.Suspended;
        await _db.SaveChangesAsync();

        var result = await _sut.LoginAsync(new LoginRequest { Email = "suspended@test.com", Password = "SuperSecret1" }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Suspended", result.Value!.Response.Player.Status);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenNoLongerWorks()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest("refresh@test.com"), CancellationToken.None);
        var originalRefreshToken = register.Value!.RawRefreshToken;

        var firstRefresh = await _sut.RefreshAsync(originalRefreshToken, CancellationToken.None);
        Assert.True(firstRefresh.Success);
        Assert.NotEqual(originalRefreshToken, firstRefresh.Value!.RawRefreshToken);

        var secondRefresh = await _sut.RefreshAsync(firstRefresh.Value.RawRefreshToken, CancellationToken.None);
        Assert.True(secondRefresh.Success);
    }

    [Fact]
    public async Task Refresh_ReuseOfRevokedToken_RevokesAllActiveTokens_ForThatPlayer()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest("reuse@test.com"), CancellationToken.None);
        var firstToken = register.Value!.RawRefreshToken;

        var rotated = await _sut.RefreshAsync(firstToken, CancellationToken.None);
        Assert.True(rotated.Success);
        var secondToken = rotated.Value!.RawRefreshToken;

        // Çalıntı/eski token tekrar kullanılıyor (reuse) — tüm aktif token'lar iptal edilmeli.
        var reuseAttempt = await _sut.RefreshAsync(firstToken, CancellationToken.None);
        Assert.False(reuseAttempt.Success);
        Assert.Equal(AuthFailureReason.RefreshTokenReuseDetected, reuseAttempt.FailureReason);

        // secondToken da (reuse tespiti sırasında) iptal edilmiş olmalı.
        var secondTokenNowInvalid = await _sut.RefreshAsync(secondToken, CancellationToken.None);
        Assert.False(secondTokenNowInvalid.Success);
    }

    [Fact]
    public async Task Google_NewIdentity_NoExistingAccount_CreatesAccount_WithEmailVerifiedImmediately()
    {
        _googleValidator.Register("valid-token", new GoogleIdentity("google-1", "googleuser@test.com", "Google User"));

        var result = await _sut.GoogleAuthAsync(
            new GoogleAuthRequest { IdToken = "valid-token", AgeConfirmed = true, TermsAccepted = true },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Value!.Response.Player.EmailVerified);
        Assert.True(result.Value.Response.Player.GoogleLinked);
        Assert.False(result.Value.Response.Player.HasPassword);
    }

    [Fact]
    public async Task Google_NewIdentity_MissingConsent_Rejected_NoAccountCreated()
    {
        _googleValidator.Register("no-consent-token", new GoogleIdentity("google-2", "noconsent@test.com", "No Consent"));

        var result = await _sut.GoogleAuthAsync(
            new GoogleAuthRequest { IdToken = "no-consent-token", AgeConfirmed = false, TermsAccepted = true },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.MissingConsent, result.FailureReason);
        Assert.False(_db.PlayerAccounts.Any(p => p.Email == "noconsent@test.com"));
    }

    [Fact]
    public async Task Google_EmailMatchesExistingPasswordAccount_ReturnsConflict_DoesNotAutoLink()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("existing@test.com"), CancellationToken.None);
        _googleValidator.Register("conflict-token", new GoogleIdentity("google-3", "existing@test.com", "Existing"));

        var result = await _sut.GoogleAuthAsync(
            new GoogleAuthRequest { IdToken = "conflict-token", AgeConfirmed = true, TermsAccepted = true },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.EmailExistsLinkRequired, result.FailureReason);

        var player = _db.PlayerAccounts.Single(p => p.Email == "existing@test.com");
        Assert.Null(player.GoogleId);
    }

    [Fact]
    public async Task Google_InvalidIdToken_Rejected()
    {
        var result = await _sut.GoogleAuthAsync(
            new GoogleAuthRequest { IdToken = "not-a-real-token", AgeConfirmed = true, TermsAccepted = true },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.GoogleIdentityInvalid, result.FailureReason);
    }

    [Fact]
    public async Task LinkGoogle_AfterPasswordLogin_AllowsBothLoginMethodsAfterward()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest("linkme@test.com"), CancellationToken.None);
        var playerId = Guid.Parse(register.Value!.Response.Player.Id);
        _googleValidator.Register("link-token", new GoogleIdentity("google-4", "irrelevant@test.com", "Link Me"));

        var linkResult = await _sut.LinkGoogleAsync(playerId, new GoogleLinkRequest { IdToken = "link-token" }, CancellationToken.None);
        Assert.True(linkResult.Success);
        Assert.True(linkResult.Value!.GoogleLinked);
        Assert.True(linkResult.Value.HasPassword);

        var passwordLogin = await _sut.LoginAsync(new LoginRequest { Email = "linkme@test.com", Password = "SuperSecret1" }, CancellationToken.None);
        Assert.True(passwordLogin.Success);

        var googleLogin = await _sut.GoogleAuthAsync(
            new GoogleAuthRequest { IdToken = "link-token" }, CancellationToken.None);
        Assert.True(googleLogin.Success);
        Assert.Equal(playerId.ToString(), googleLogin.Value!.Response.Player.Id);
    }

    [Fact]
    public async Task LinkGoogle_AlreadyLinkedToAnotherAccount_Rejected()
    {
        var registerA = await _sut.RegisterAsync(ValidRegisterRequest("playerA@test.com"), CancellationToken.None);
        var registerB = await _sut.RegisterAsync(ValidRegisterRequest("playerB@test.com"), CancellationToken.None);
        _googleValidator.Register("shared-token", new GoogleIdentity("google-shared", "shared@test.com", "Shared"));

        var firstLink = await _sut.LinkGoogleAsync(Guid.Parse(registerA.Value!.Response.Player.Id), new GoogleLinkRequest { IdToken = "shared-token" }, CancellationToken.None);
        Assert.True(firstLink.Success);

        var secondLink = await _sut.LinkGoogleAsync(Guid.Parse(registerB.Value!.Response.Player.Id), new GoogleLinkRequest { IdToken = "shared-token" }, CancellationToken.None);
        Assert.False(secondLink.Success);
        Assert.Equal(AuthFailureReason.GoogleAlreadyLinked, secondLink.FailureReason);
    }

    [Fact]
    public async Task ForgotPassword_Then_ResetPassword_AllowsLoginWithNewPassword_AndRevokesOldSessions()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest("reset@test.com"), CancellationToken.None);
        var oldRefreshToken = register.Value!.RawRefreshToken;

        await _sut.ForgotPasswordAsync("reset@test.com", CancellationToken.None);
        var resetEmail = Assert.Single(_emailSender.SentEmails, e => e.Subject.Contains("Şifre Sıfırlama"));
        var rawToken = resetEmail.BodyText.Split('/').Last();

        var resetResult = await _sut.ResetPasswordAsync(rawToken, "BrandNewPassword1", CancellationToken.None);
        Assert.True(resetResult.Success);

        var oldPasswordLogin = await _sut.LoginAsync(new LoginRequest { Email = "reset@test.com", Password = "SuperSecret1" }, CancellationToken.None);
        Assert.False(oldPasswordLogin.Success);

        var newPasswordLogin = await _sut.LoginAsync(new LoginRequest { Email = "reset@test.com", Password = "BrandNewPassword1" }, CancellationToken.None);
        Assert.True(newPasswordLogin.Success);

        // Parola değişince eski oturumun refresh token'ı da iptal edilmiş olmalı.
        var oldSessionRefresh = await _sut.RefreshAsync(oldRefreshToken, CancellationToken.None);
        Assert.False(oldSessionRefresh.Success);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Rejected()
    {
        var result = await _sut.ResetPasswordAsync("not-a-real-token", "BrandNewPassword1", CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.InvalidToken, result.FailureReason);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_Rejected()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("expired@test.com"), CancellationToken.None);
        await _sut.ForgotPasswordAsync("expired@test.com", CancellationToken.None);
        var email = Assert.Single(_emailSender.SentEmails, e => e.Subject.Contains("Şifre Sıfırlama"));
        var rawToken = email.BodyText.Split('/').Last();

        _timeProvider.Advance(TimeSpan.FromSeconds(_config.PasswordResetTokenExpirySeconds + 1));

        var result = await _sut.ResetPasswordAsync(rawToken, "BrandNewPassword1", CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.TokenExpired, result.FailureReason);
    }

    [Fact]
    public async Task ChangePassword_GoogleOnlyAccount_DoesNotRequireCurrentPassword()
    {
        _googleValidator.Register("google-only-token", new GoogleIdentity("google-only", "googleonly@test.com", "Google Only"));
        var register = await _sut.GoogleAuthAsync(
            new GoogleAuthRequest { IdToken = "google-only-token", AgeConfirmed = true, TermsAccepted = true },
            CancellationToken.None);
        var playerId = Guid.Parse(register.Value!.Response.Player.Id);

        var result = await _sut.ChangePasswordAsync(
            playerId, new ChangePasswordRequest { CurrentPassword = null, NewPassword = "FirstPassword1" }, CancellationToken.None);

        Assert.True(result.Success);
        var login = await _sut.LoginAsync(new LoginRequest { Email = "googleonly@test.com", Password = "FirstPassword1" }, CancellationToken.None);
        Assert.True(login.Success);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Rejected()
    {
        var register = await _sut.RegisterAsync(ValidRegisterRequest("changepw@test.com"), CancellationToken.None);
        var playerId = Guid.Parse(register.Value!.Response.Player.Id);

        var result = await _sut.ChangePasswordAsync(
            playerId, new ChangePasswordRequest { CurrentPassword = "WrongOne1", NewPassword = "NewPassword1" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AuthFailureReason.InvalidCredentials, result.FailureReason);
    }

    [Fact]
    public async Task VerifyEmail_ValidToken_MarksEmailVerified()
    {
        await _sut.RegisterAsync(ValidRegisterRequest("verify@test.com"), CancellationToken.None);
        var email = Assert.Single(_emailSender.SentEmails, e => e.Subject.Contains("E-posta Doğrulama"));
        var rawToken = email.BodyText.Split('/').Last();

        var result = await _sut.VerifyEmailAsync(rawToken, CancellationToken.None);

        Assert.True(result.Success);
        var player = _db.PlayerAccounts.Single(p => p.Email == "verify@test.com");
        Assert.NotNull(player.EmailVerifiedAt);
    }

    [Fact]
    public async Task Guard_PasswordHashAndGoogleId_NeverBothNull()
    {
        var passwordAccount = await _sut.RegisterAsync(ValidRegisterRequest("guard1@test.com"), CancellationToken.None);
        _googleValidator.Register("guard-token", new GoogleIdentity("guard-google", "guard2@test.com", "Guard"));
        var googleAccount = await _sut.GoogleAuthAsync(
            new GoogleAuthRequest { IdToken = "guard-token", AgeConfirmed = true, TermsAccepted = true }, CancellationToken.None);

        foreach (var player in _db.PlayerAccounts)
        {
            Assert.True(player.PasswordHash is not null || player.GoogleId is not null);
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
