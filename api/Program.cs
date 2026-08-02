using api.Hubs;
using api.Services;
using api.Services.GameEngine;

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

var app = builder.Build();

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
