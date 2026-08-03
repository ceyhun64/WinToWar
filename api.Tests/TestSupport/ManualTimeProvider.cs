namespace api.Tests.TestSupport;

/// <summary>
/// Deterministik zaman testleri için minimal bir TimeProvider implementasyonu
/// (retry/backoff, cache staleness, monotonluk gibi zaman-duyarlı davranışları
/// kontrollü ilerletebilmek için). Harici bir test paketi eklemek yerine (YAGNI)
/// birkaç satırlık bu sahte implementasyon yeterlidir.
/// </summary>
public class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset start)
    {
        _utcNow = start;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}
