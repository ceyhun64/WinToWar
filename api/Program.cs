using api;
using api.Hubs;
using api.Services;
using api.Services.GameEngine;
using api.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

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
    options.AddPolicy(WebClientCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Porsuk Savaşları oyun motoru servisleri.
builder.Services.AddSingleton<MapProvider>();
builder.Services.AddSingleton<MatchManager>();
builder.Services.AddSingleton<CombatService>();
builder.Services.AddSingleton<MovementService>();
builder.Services.AddSingleton<UpgradeService>();
builder.Services.AddHostedService<EconomyTickService>();

// ---- Ödeme modülü (docs/05-payment.md) — ana oyun motorundan tamamen ayrı katman ----
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<PaymentConfig>(builder.Configuration.GetSection(PaymentConfig.SectionName));
builder.Services.AddDbContext<PaymentDbContext>((sp, options) =>
{
    var paymentConfig = sp.GetRequiredService<IOptions<PaymentConfig>>().Value;
    options.UseSqlite(paymentConfig.ConnectionString);
});

// 🛠️ User-Agent header eklenmezse CoinGecko bot koruması 403 döndürüyor —
// HttpClient'ın varsayılan (boş) User-Agent'ı buna takılıyor.
builder.Services.AddHttpClient("CoinGecko", client =>
{
    client.BaseAddress = new Uri("https://api.coingecko.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PorsukSavaslari/1.0");
});
builder.Services.AddHttpClient("CoinCap", client =>
{
    client.BaseAddress = new Uri("https://api.coincap.io/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("PorsukSavaslari/1.0");
});
builder.Services.AddSingleton<IPriceOracle>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    IExchangeRateProvider[] providers =
    [
        new CoinGeckoExchangeRateProvider(httpClientFactory.CreateClient("CoinGecko")),
        new CoinCapExchangeRateProvider(httpClientFactory.CreateClient("CoinCap"))
    ];
    return new CompositePriceOracle(
        providers,
        sp.GetRequiredService<IOptions<PaymentConfig>>(),
        sp.GetRequiredService<TimeProvider>(),
        sp.GetRequiredService<ILogger<CompositePriceOracle>>());
});

// 🛠️ Bölüm 0.3 ön koşulu: BTCPay regtest/testnet erişilemediği için sahte
// implementasyonla ilerlendi (bkz. Payments/Providers/FakePaymentProvider.cs).
builder.Services.AddSingleton<IPaymentProvider, FakePaymentProvider>();

builder.Services.AddScoped<PaymentEventNotifier>();
builder.Services.AddScoped<RefundService>();
builder.Services.AddScoped<PayoutService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHostedService<ReconciliationService>();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    startupScope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreated();
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

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
