using System.Text;
using api;
using api.Hubs;
using api.Models.Auth;
using api.Services;
using api.Services.Auth;
using api.Services.GameEngine;
using api.Services.Matchmaking;
using api.Services.Payments;
using api.Services.Rooms;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// docs/07-pages.md `/admin/loglar`: mevcut ILogger çıktısının son N kaydını
// bellekte tutan salt-okunur bir sağlayıcı — builder.Build() öncesi oluşturulur
// ki hem DI'a (controller'lar için) hem de logging pipeline'ına aynı instance
// verilebilsin. AdminConfig henüz DI konteynerine bağlanmadığından (Build()
// öncesi), MaxLogEntries doğrudan builder.Configuration'dan okunur.
var adminMaxLogEntries = builder.Configuration.GetValue("Admin:MaxLogEntries", 500);
var adminLogStore = new InMemoryLogStore(adminMaxLogEntries);
builder.Services.AddSingleton(adminLogStore);
builder.Logging.AddProvider(new InMemoryLoggerProvider(adminLogStore));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Next.js dev sunucusunun (localhost:3000) SignalR/REST'e erişebilmesi için.
// Credentials gerekli çünkü SignalR bağlantısı cookie/negotiate akışı kullanabilir.
const string WebClientCorsPolicy = "WebClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        WebClientCorsPolicy,
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:3000", 
                    "https://win-to-war.vercel.app"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    );
});

// WinToWar oyun motoru servisleri.
builder.Services.AddSingleton<MapProvider>();
builder.Services.AddSingleton<MatchEventLogWriter>();
// 🛠️ Scoped: GameEventDbContext (Scoped) wrap eder — bkz. MatchEventLogReader.cs.
builder.Services.AddScoped<MatchEventLogReader>();
builder.Services.AddSingleton<MatchManager>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<SupportTicketStore>();
builder.Services.Configure<AdminConfig>(builder.Configuration.GetSection(AdminConfig.SectionName));
builder.Services.AddSingleton<CombatService>();
builder.Services.AddSingleton<MovementService>();
// docs/03-game-rules.md Bölüm 7 (DÜZELTME) / Bölüm 13: bot eşleştirme+AI servisi.
builder.Services.AddSingleton<BotMatchService>();
builder.Services.AddHostedService<EconomyTickService>();
builder.Services.AddHostedService<MatchEventLogFlushService>();

// ---- Ödeme modülü (docs/05-payment.md) — ana oyun motorundan tamamen ayrı katman ----
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<PaymentConfig>(
    builder.Configuration.GetSection(PaymentConfig.SectionName)
);
builder.Services.AddDbContext<PaymentDbContext>(
    (sp, options) =>
    {
        var paymentConfig = sp.GetRequiredService<IOptions<PaymentConfig>>().Value;
        options.UseNpgsql(paymentConfig.ConnectionString);
    }
);

// docs/02-architecture.md "Maç Denetim Kaydı": oyun motorunun tek kalıcı deposu —
// 🛠️ tek-instance/tek-Postgres varsayımı (bkz. "Ölçeklenebilirlik") gereği aynı
// connection string'i paylaşır, ama ayrı bir DbContext/tablo kümesidir (Payments
// modülünden bağımsız, bkz. GameEventDbContext).
builder.Services.AddDbContext<GameEventDbContext>(
    (sp, options) =>
    {
        var paymentConfig = sp.GetRequiredService<IOptions<PaymentConfig>>().Value;
        options.UseNpgsql(paymentConfig.ConnectionString);
    }
);

// 🛠️ User-Agent header eklenmezse CoinGecko bot koruması 403 döndürüyor —
// HttpClient'ın varsayılan (boş) User-Agent'ı buna takılıyor.
builder.Services.AddHttpClient(
    "CoinGecko",
    client =>
    {
        client.BaseAddress = new Uri("https://api.coingecko.com/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WinToWar/1.0");
    }
);
builder.Services.AddHttpClient(
    "CoinCap",
    client =>
    {
        client.BaseAddress = new Uri("https://api.coincap.io/");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WinToWar/1.0");
    }
);
builder.Services.AddSingleton<IPriceOracle>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    IExchangeRateProvider[] providers =
    [
        new CoinGeckoExchangeRateProvider(httpClientFactory.CreateClient("CoinGecko")),
        new CoinCapExchangeRateProvider(httpClientFactory.CreateClient("CoinCap")),
    ];
    return new CompositePriceOracle(
        providers,
        sp.GetRequiredService<IOptions<PaymentConfig>>(),
        sp.GetRequiredService<TimeProvider>(),
        sp.GetRequiredService<ILogger<CompositePriceOracle>>()
    );
});

// BtcPayGreenfieldProvider: BaseAddress/API key config'ten (Payment.BtcPay*)
// okunur, o yüzden ConfigureHttpClient((sp, client) => ...) overload'u kullanılır
// (basit AddHttpClient(name, client => ...) formunun aksine, DI'a erişim gerekir).
builder.Services.AddTransient<PaymentProviderTransportHandler>();
builder.Services.AddHttpClient("BtcPay").ConfigureHttpClient((sp, client) =>
{
    var paymentConfig = sp.GetRequiredService<IOptions<PaymentConfig>>().Value;
    client.BaseAddress = new Uri(paymentConfig.BtcPayBaseUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"token {paymentConfig.BtcPayApiKey}");
})
// Sağlayıcıya hiç ulaşılamadığında (bağlantı reddi/DNS/timeout) 500 + stack trace
// yerine tipli bir 503 dönebilmek için — bkz. PaymentProviderTransportHandler.
.AddHttpMessageHandler<PaymentProviderTransportHandler>();

// docs/21-payment-sandbox-e2e.md Bölüm 5: provider seçimi ASP.NET ortamına değil,
// Payment:Mode (Fake/Sandbox/Live) enum'ına bağlıdır. Tanımsızsa Fake'tir.
// 🔒 Sandbox ile Live arasında KOD FARKI YOKTUR — ikisi de aynı
// BtcPayGreenfieldProvider'ı aynı şekilde kullanır, yalnızca config değerleri
// (BtcPayBaseUrl/ApiKey/StoreId/WebhookSecret) farklıdır. Aşağıdaki tek dallanma
// "sahte mi, gerçek mi" sorusunu ayırır; Sandbox'a özel bir davranış yoktur.
var paymentConfigForProviderSelection = builder.Configuration.GetSection(PaymentConfig.SectionName).Get<PaymentConfig>() ?? new PaymentConfig();
if (paymentConfigForProviderSelection.Mode == PaymentProviderMode.Fake)
{
    builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();
}
else
{
    // 🔒 Bölüm 5 fail-fast: gerçek bir BTCPay'e bağlanacak her modda (Sandbox ve
    // Live) bağlantı bilgileri eksikse uygulama BAŞLAMAZ. Sessizce Fake'e düşmek
    // (gerçek para taşıyan bir sistemde prod'a sahte provider'la çıkmak) en
    // tehlikeli senaryodur, bu yüzden kesinlikle yasaktır. Doğrulama Sandbox ve
    // Live için AYNIDIR — mod başına farklı bir kural yazmak, "Sandbox→Live geçişi
    // config-only" garantisini bozardı.
    var missingBtcPayFields = paymentConfigForProviderSelection.GetMissingBtcPayFieldNames();

    if (missingBtcPayFields.Count > 0)
    {
        throw new InvalidOperationException(
            $"Payment:Mode={paymentConfigForProviderSelection.Mode} için zorunlu BTCPay ayarları eksik: " +
            $"{string.Join(", ", missingBtcPayFields)}. Uygulama sahte provider'a düşmez, başlatılmıyor.");
    }

    builder.Services.AddSingleton<IPaymentProvider, BtcPayGreenfieldProvider>();
}

builder.Services.AddScoped<PaymentEventNotifier>();
builder.Services.AddScoped<RefundService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<RoomEntryService>();
builder.Services.AddScoped<PaymentService>();

// ---- Kimlik doğrulama modülü (docs/11-auth.md) — ödeme/oyun motorundan ayrı, üçüncü bir katman ----
builder.Services.Configure<AuthConfig>(builder.Configuration.GetSection(AuthConfig.SectionName));
builder.Services.AddDbContext<AuthDbContext>(
    (sp, options) =>
    {
        var authConfig = sp.GetRequiredService<IOptions<AuthConfig>>().Value;
        options.UseNpgsql(authConfig.ConnectionString);
    }
);
builder.Services.AddSingleton<AuthRateLimiter>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddScoped<AuthService>();

var authConfigForJwt = builder.Configuration.GetSection(AuthConfig.SectionName).Get<AuthConfig>() ?? new AuthConfig();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authConfigForJwt.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = authConfigForJwt.JwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authConfigForJwt.JwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // docs/11-auth.md Bölüm 1.4/3.5: SignalR handshake header taşıyamaz, access_token
        // query param'ı üzerinden okunur (ASP.NET Core'un resmi SignalR+JWT deseni).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hub"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    // 06-coding-standards.md#Veritabanı/Migration Disiplini: şema migration
    // tabanlı yönetilir, EnsureCreated() (izlenemez/geri alınamaz) kullanılmaz.
    startupScope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.Migrate();
    startupScope.ServiceProvider.GetRequiredService<GameEventDbContext>().Database.Migrate();
    startupScope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.Migrate();

    // docs/11-auth.md Bölüm 1.9: ilk admin self-servis kayıttan oluşturulamaz,
    // yalnızca env var/appsettings'ten (SeedAdminEmail/SeedAdminPassword) okunur
    // ve yalnızca eşleşen bir Player yoksa bir kez oluşturulur.
    var authConfig = startupScope.ServiceProvider.GetRequiredService<IOptions<AuthConfig>>().Value;
    if (!string.IsNullOrWhiteSpace(authConfig.SeedAdminEmail) && !string.IsNullOrWhiteSpace(authConfig.SeedAdminPassword))
    {
        var authDb = startupScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var seedEmail = authConfig.SeedAdminEmail.Trim().ToLowerInvariant();
        if (!authDb.PlayerAccounts.Any(p => p.Email == seedEmail))
        {
            var admin = new PlayerAccount
            {
                Email = seedEmail,
                DisplayName = "Admin",
                Role = PlayerRole.Admin,
                Status = PlayerStatus.Active,
                EmailVerifiedAt = DateTime.UtcNow,
                AgeConfirmedAt = DateTime.UtcNow,
                TermsAcceptedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            admin.PasswordHash = new PasswordHasher<PlayerAccount>().HashPassword(admin, authConfig.SeedAdminPassword);
            authDb.PlayerAccounts.Add(admin);
            authDb.SaveChanges();
        }
    }

    // docs/03-game-rules.md Bölüm 2.2: oda kapasiteleri haritadaki bölge sayısını
    // aşamaz — aşarsa "N oyuncu, N'den az bölge" gibi imkânsız bir kombinasyona
    // sessizce izin verilmiş olur, bu yüzden açılışta yakalanır. 🛠️ docs/09-eksik-tarama-promptu.md
    // denetimi (Faz 5): önceden yalnızca VIP/Standart kontrol ediliyordu, Practice
    // eksikti — eklendi. Bilinçli tercih: bu, RoomService'te sessizce Math.Min(...)
    // ile kapasiteyi kısan bir "runtime clamp" DEĞİLDİR — Standart/Practice'in
    // oyuncu sayısı müşteri tarafından 🔒 verilmiş sabit değerlerdir (bkz.
    // `03-game-rules.md`), bunları harita küçültüldüğünde sessizce değiştirmek
    // müşteri kararını ihlal eder; bunun yerine uygulama başlangıçta AÇIKÇA çöker
    // ki yanlış yapılandırma hiçbir zaman sessizce prod'a çıkmasın.
    var regionCount = startupScope.ServiceProvider.GetRequiredService<MapProvider>().RegionCount;
    if (GameConfig.VipRoomMaxPlayers > regionCount ||
        GameConfig.StandardRoomPlayerCount > regionCount ||
        GameConfig.PracticeRoomDefaultPlayerCount > regionCount)
    {
        throw new InvalidOperationException(
            $"Oda kapasiteleri (VIP={GameConfig.VipRoomMaxPlayers}, Standart={GameConfig.StandardRoomPlayerCount}, " +
            $"Practice={GameConfig.PracticeRoomDefaultPlayerCount}) haritadaki bölge sayısını ({regionCount}) aşıyor.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(WebClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hub/game");
app.MapHub<WalletHub>("/hub/wallet");

// docs/07-pages.md `/durum`: her bileşen için basit bir health-check — karmaşık
// bir monitoring sistemi kurulmaz (YAGNI), yalnızca API'nin ayakta olduğunu ve
// veritabanına erişilebildiğini raporlar.
app.MapGet("/api/health", async (PaymentDbContext db) =>
{
    var databaseOk = await db.Database.CanConnectAsync();
    return Results.Ok(new { api = true, database = databaseOk });
});

app.Run();

// docs/11-auth.md Bölüm 8 "PlayerId güvenliği" testi: WebApplicationFactory<Program>
// (api.Tests) Program sınıfına erişebilmek için bunu public yapar — top-level
// statement'lar varsayılan olarak internal bir Program sınıfı üretir.
public partial class Program;
