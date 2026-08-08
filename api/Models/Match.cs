using api.Models.Rooms;

namespace api.Models;

/// <summary>
/// docs/03-game-rules.md "Kazanma koşulu": ayrı bir Drawn durumu yoktur, beraberlik
/// Winners'ın birden fazla eleman içermesiyle temsil edilir (Bölüm 8, eşzamanlı eleme).
/// </summary>
public enum MatchStatus
{
    Lobby,
    Countdown,
    Playing,
    Completed,
    Cancelled
}

/// <summary>
/// Bir maçın tüm çalışma zamanı durumu. Lock, EconomyTickService'in periyodik
/// tick'i ile GameHub üzerinden gelen oyuncu aksiyonlarının aynı anda bu state'i
/// mutasyona uğratmasını engellemek için kullanılır (bkz. MatchManager).
/// </summary>
public class Match
{
    public required string Id { get; init; }
    public required Room Room { get; init; }
    public List<Player> Players { get; } = new();
    public Dictionary<string, Region> Regions { get; } = new();
    public List<Army> Armies { get; } = new();

    /// <summary>docs/03-game-rules.md Bölüm 10 tie-break kuralı için — bkz. Army.SequenceNo.</summary>
    public long NextArmySequenceNo { get; set; }
    public MatchStatus Status { get; set; } = MatchStatus.Lobby;

    public DateTime? LobbyOpenedAtUtc { get; set; }

    /// <summary>docs/03-game-rules.md Bölüm 7: 300 sn (Practice'te 60 sn) dolduğunda bir kez bildirilir; lobi otomatik iptal olmaz.</summary>
    public bool LobbyTimeoutNotified { get; set; }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 7 (DÜZELTME — bot politikası): lobiye ilk gerçek
    /// oyuncu girdiğinde GameConfig.BotMatchWaitMinSeconds-MaxSeconds arası rastgele
    /// bir süre sonrasına ayarlanır (yalnızca Standart/Practice — VIP'de asla).
    /// Bu zamana kadar lobi dolmazsa BotMatchService kalan koltukları bot ile doldurur.
    /// </summary>
    public DateTime? BotFillDeadlineUtc { get; set; }

    public DateTime? CountdownEndsAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Normal senaryoda tek eleman; eşzamanlı eleme durumunda ortak kazananların tamamını içerir.</summary>
    public List<string> Winners { get; } = new();

    public readonly object Lock = new();
}
