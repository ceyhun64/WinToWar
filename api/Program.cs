using api;
using api.Hubs;
using api.Services;
using api.Services.GameEngine;
using api.Services.Payments;
using api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
                .WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    );
});

// WinToWar oyun motoru servisleri.
builder.Services.AddSingleton<MapProvider>();
builder.Services.AddSingleton<MatchEventLogWriter>();
builder.Services.AddSingleton<MatchManager>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddSingleton<SupportTicketStore>();
builder.Services.Configure<AdminConfig>(builder.Configuration.GetSection(AdminConfig.SectionName));
builder.Services.AddSingleton<CombatService>();
builder.Services.AddSingleton<MovementService>();
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

// 🛠️ Bölüm 0.3 ön koşulu: BTCPay regtest/testnet erişilemediği için sahte
// implementasyonla ilerlendi (bkz. Payments/Providers/FakePaymentProvider.cs).
builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();

builder.Services.AddScoped<PaymentEventNotifier>();
builder.Services.AddScoped<RefundService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<RoomEntryService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHostedService<ReconciliationService>();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    startupScope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreated();
    startupScope.ServiceProvider.GetRequiredService<GameEventDbContext>().Database.EnsureCreated();

    // docs/03-game-rules.md Bölüm 2.2: oda kapasiteleri haritadaki bölge sayısını
    // aşamaz — aşarsa "N oyuncu, N'den az bölge" gibi imkânsız bir kombinasyona
    // sessizce izin verilmiş olur, bu yüzden açılışta yakalanır.
    var regionCount = startupScope.ServiceProvider.GetRequiredService<MapProvider>().RegionCount;
    if (GameConfig.VipRoomMaxPlayers > regionCount || GameConfig.StandardRoomPlayerCount > regionCount)
    {
        throw new InvalidOperationException(
            $"Oda kapasiteleri (VIP={GameConfig.VipRoomMaxPlayers}, Standart={GameConfig.StandardRoomPlayerCount}) haritadaki bölge sayısını ({regionCount}) aşıyor.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(WebClientCorsPolicy);

app.MapControllers();
app.MapHub<GameHub>("/hub/game");

// docs/07-pages.md `/durum`: her bileşen için basit bir health-check — karmaşık
// bir monitoring sistemi kurulmaz (YAGNI), yalnızca API'nin ayakta olduğunu ve
// veritabanına erişilebildiğini raporlar.
app.MapGet("/api/health", async (PaymentDbContext db) =>
{
    var databaseOk = await db.Database.CanConnectAsync();
    return Results.Ok(new { api = true, database = databaseOk });
});

app.Run();
