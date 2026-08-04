namespace api.Models;

/// <summary>
/// docs/02-architecture.md "Maç Denetim Kaydı (Audit Log)": ödeme itirazlarında
/// ("maç gerçekten bu şekilde bitti mi") kanıt olarak gösterilebilecek ham bir
/// kayıt — replay/izleme özelliği değildir, yalnızca destek amaçlı minimal veri.
/// </summary>
public enum MatchEventType
{
    MatchStarted,
    RegionAttacked,
    RegionCaptured,
    PlayerEliminated,
    MatchEnded
}

/// <summary>
/// Yalnızca destek/itiraz amaçlı sorgulanır (`/admin/maclar`), genel kullanıcıya
/// (`/gecmis`) gösterilmez. Sıcak yol performansını etkilememesi için senkron
/// değil, <see cref="Services.MatchEventLogWriter"/> üzerinden fire-and-forget/buffer
/// olarak yazılır (bkz. o sınıftaki açıklama).
/// </summary>
public class MatchEventLog
{
    public Guid Id { get; init; }
    public required string MatchId { get; init; }
    public required int SequenceNo { get; init; }
    public required MatchEventType EventType { get; init; }
    public required string PayloadJson { get; init; }
    public required DateTime OccurredAt { get; init; }
}
