namespace api.Models;

public enum MatchStatus
{
    WaitingForPlayers,
    InProgress,
    Finished
}

/// <summary>
/// Bir maçın tüm çalışma zamanı durumu. Lock, EconomyTickService'in periyodik
/// tick'i ile GameHub üzerinden gelen oyuncu aksiyonlarının aynı anda bu state'i
/// mutasyona uğratmasını engellemek için kullanılır (bkz. MatchManager).
/// </summary>
public class Match
{
    public required string Id { get; init; }
    public List<Player> Players { get; } = new();
    public Dictionary<string, Region> Regions { get; } = new();
    public List<General> Generals { get; } = new();
    public List<Army> Armies { get; } = new();
    public MatchStatus Status { get; set; } = MatchStatus.WaitingForPlayers;
    public DateTime? StartedAtUtc { get; set; }
    public string? WinnerId { get; set; }

    public readonly object Lock = new();
}
