using Microsoft.EntityFrameworkCore;

namespace api.Services;

/// <summary>
/// docs/02-architecture.md "Maç Denetim Kaydı": <see cref="MatchEventLogWriter"/>
/// içindeki buffer'ı periyodik olarak boşaltıp toplu INSERT yapar (fire-and-forget
/// yazım burada gerçek kalıcılığa dönüşür); ayrıca GameConfig.MatchEventLogRetentionDays'i
/// aşan eski kayıtları aynı tick'te temizler (ayrı bir hosted service açmak yerine
/// YAGNI — tek bir periyodik iş tek bir servistedir).
/// </summary>
public class MatchEventLogFlushService : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    private readonly MatchEventLogWriter _writer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MatchEventLogFlushService> _logger;

    public MatchEventLogFlushService(
        MatchEventLogWriter writer,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<MatchEventLogFlushService> logger)
    {
        _writer = writer;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await FlushAsync(stoppingToken);
            await CleanupRetentionAsync(stoppingToken);
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        var batch = new List<Models.MatchEventLog>();
        while (batch.Count < 500 && _writer.Reader.TryRead(out var entry))
        {
            batch.Add(entry);
        }

        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameEventDbContext>();
            db.MatchEventLogs.AddRange(batch);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MatchEventLog toplu yazımı başarısız, {Count} kayıt kaybedildi", batch.Count);
        }
    }

    private async Task CleanupRetentionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-GameConfig.MatchEventLogRetentionDays);
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameEventDbContext>();
            await db.MatchEventLogs.Where(e => e.OccurredAt < cutoff).ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MatchEventLog retention temizliği başarısız");
        }
    }
}
