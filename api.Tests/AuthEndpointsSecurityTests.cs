using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using api.Models.Auth.Dtos;
using api.Models.Payments.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace api.Tests;

/// <summary>
/// docs/11-auth.md Bölüm 0.4/8 "PlayerId güvenliği" — [Authorize]/JWT middleware'inin
/// gerçek HTTP pipeline'ında çalıştığını, PlayerId'nin yalnızca JWT'den okunduğunu
/// (client'tan asla alınmadığını) uçtan uca doğrular. Servis-seviyesi testler
/// (AuthServiceTests) middleware'i hiç devreye sokmadığı için bu ayrı bir katmandır.
/// Gerçek Postgres'e karşı çalışır (bkz. AuthApiFactory) — Payments/GameEvents/Auth
/// modüllerinin hepsi migrate edilir, ayrı bir test veritabanı kullanılır ki gerçek
/// geliştirme verisine dokunulmasın.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            const string testConnectionString =
                "Host=localhost;Port=5432;Database=wintowar_authtest;Username=postgres;Password=postgres";
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:ConnectionString"] = testConnectionString,
                ["Payment:ConnectionString"] = testConnectionString,
                // Testler kendi hesaplarını oluşturur, sabit bir admin seed'i gerekmez.
                ["Auth:SeedAdminEmail"] = "",
                ["Auth:SeedAdminPassword"] = "",
            });
        });
    }
}

public class AuthEndpointsSecurityTests : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory;

    public AuthEndpointsSecurityTests(AuthApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<(HttpClient Client, PlayerAccountDto Player)> RegisterAndAuthenticateAsync(HttpClient anonymousClientFactory, string email)
    {
        var client = anonymousClientFactory;
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "SuperSecret1",
            displayName = "Test Player",
            ageConfirmed = true,
            termsAccepted = true,
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        return (client, body!.Player);
    }

    [Fact]
    public async Task WalletBalance_WithoutAuthorizationHeader_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/wallet");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyWalletRouteWithPlayerIdSegment_NoLongerExists()
    {
        var client = _factory.CreateClient();

        // docs/11-auth.md Bölüm 0.4: eski /api/wallet/{playerId} rotası kaldırıldı —
        // bir playerId'yi URL'e gömerek başka bir hesabın bakiyesini sorgulamak
        // artık mümkün değil (route bulunamaz, 404).
        var response = await client.GetAsync($"/api/wallet/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WalletBalance_ReturnsOnlyTheAuthenticatedPlayersOwnWallet()
    {
        var clientA = _factory.CreateClient();
        var (_, playerA) = await RegisterAndAuthenticateAsync(clientA, $"playerA-{Guid.NewGuid():N}@test.com");
        var authA = await AuthenticateAsync(clientA, playerA.Email);

        var clientB = _factory.CreateClient();
        var (_, playerB) = await RegisterAndAuthenticateAsync(clientB, $"playerB-{Guid.NewGuid():N}@test.com");
        var authB = await AuthenticateAsync(clientB, playerB.Email);

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);

        // A kendi bakiyesine top-up yapar.
        var topUp = await clientA.PostAsJsonAsync("/api/wallet/topup", new { amountUsd = 5.00m });
        topUp.EnsureSuccessStatusCode();

        var walletA = await (await clientA.GetAsync("/api/wallet")).Content.ReadFromJsonAsync<WalletDto>();
        var walletB = await (await clientB.GetAsync("/api/wallet")).Content.ReadFromJsonAsync<WalletDto>();

        // B kendi (sıfır) bakiyesini görür — A'nın JWT'sini kullanmadan A'nın
        // verisine hiçbir şekilde erişemez (id, body/query'de taşınmadığından
        // "başka bir playerId gönderme" saldırısı yapısal olarak imkânsızdır).
        Assert.Equal(playerA.Id, walletA!.PlayerId);
        Assert.Equal(playerB.Id, walletB!.PlayerId);
        Assert.NotEqual(walletA.PlayerId, walletB.PlayerId);
        Assert.Equal("0.00", walletB.BalanceUsd);
    }

    [Fact]
    public async Task Register_Login_Refresh_Logout_FullHttpFlow()
    {
        // Refresh cookie'si HttpOnly+Secure+SameSite=Strict taşır (docs/11-auth.md Bölüm 1.4).
        // CookieContainer'ın Secure/domain eşleştirmesine güvenmek yerine (TestServer'ın
        // sanal host'unda kırılgan) Set-Cookie değeri elle okunup sonraki isteğe elle
        // Cookie header'ı olarak eklenir — davranış aynı, yalnızca test daha güvenilir.
        var client = _factory.CreateClient();
        var email = $"flow-{Guid.NewGuid():N}@test.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "SuperSecret1",
            displayName = "Flow Player",
            ageConfirmed = true,
            termsAccepted = true,
        });
        registerResponse.EnsureSuccessStatusCode();
        var refreshCookie = ExtractCookie(registerResponse, "wintowar_refresh");
        Assert.NotNull(refreshCookie);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", refreshCookie);
        var refreshResponse = await client.SendAsync(refreshRequest);
        refreshResponse.EnsureSuccessStatusCode();
        var refreshedAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(refreshedAuth);
        var rotatedCookie = ExtractCookie(refreshResponse, "wintowar_refresh");
        Assert.NotNull(rotatedCookie);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedAuth!.AccessToken);
        var meResponse = await client.GetAsync("/api/auth/me");
        meResponse.EnsureSuccessStatusCode();

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", rotatedCookie);
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedAuth.AccessToken);
        var logoutResponse = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Logout sonrası aynı refresh cookie'siyle tekrar refresh denemesi reddedilmeli.
        var refreshAfterLogoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshAfterLogoutRequest.Headers.Add("Cookie", rotatedCookie);
        var refreshAfterLogout = await client.SendAsync(refreshAfterLogoutRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    private static string? ExtractCookie(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith($"{cookieName}=", StringComparison.Ordinal))
            {
                return cookie.Split(';')[0];
            }
        }

        return null;
    }

    private static async Task<AuthResponseDto> AuthenticateAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "SuperSecret1" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
    }
}
