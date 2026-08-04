using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using api.Models;

namespace api.Services;

/// <summary>
/// docs/02-architecture.md "Maç Denetim Kaydı": kayıt sıcak yol performansını
/// etkilememesi için senkron değil fire-and-forget yapılır — <see cref="Log"/>
/// çağrısı yalnızca bir bounded channel'a yazar (asla bloklamaz, asla exception
/// fırlatmaz), asıl DB yazımı <see cref="MatchEventLogFlushService"/> tarafından
/// arka planda toplu (buffer) yapılır. Sıra numarası (SequenceNo) maç başına
/// tek bu sınıfta, thread-safe artan bir sayaçla üretilir — tek doğruluk kaynağı.
/// </summary>
public class MatchEventLogWriter
{
    private readonly Channel<MatchEventLog> _channel = Channel.CreateBounded<MatchEventLog>(
        new BoundedChannelOptions(capacity: 4096) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly ConcurrentDictionary<string, int> _sequenceByMatchId = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MatchEventLogWriter> _logger;

    public MatchEventLogWriter(TimeProvider timeProvider, ILogger<MatchEventLogWriter> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public ChannelReader<MatchEventLog> Reader => _channel.Reader;

    public void Log(string matchId, MatchEventType eventType, object payload)
    {
        var sequenceNo = _sequenceByMatchId.AddOrUpdate(matchId, 1, (_, current) => current + 1);
        var entry = new MatchEventLog
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            SequenceNo = sequenceNo,
            EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(payload),
            OccurredAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        if (!_channel.Writer.TryWrite(entry))
        {
            _logger.LogWarning("MatchEventLog buffer dolu, kayıt düşürüldü: {MatchId}, {EventType}", matchId, eventType);
        }
    }
}
