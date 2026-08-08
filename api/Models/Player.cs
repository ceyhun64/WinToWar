namespace api.Models;

public enum PlayerConnectionStatus
{
    Connected,
    Disconnected
}

/// <summary>
/// Bir maça katılmış oyuncu. WinToWar'da ayrı bir Altın kaynağı yoktur (bkz.
/// docs/03-game-rules.md Bölüm 4) — tek kaynak askerdir, bu da bölgelerde
/// (Region.SoldierCount) tutulur, Player üzerinde ayrı bir sayaç yoktur.
/// </summary>
public class Player
{
    public required string Id { get; init; }
    public required int Slot { get; init; }
    public required string Name { get; init; }
    public string? ConnectionId { get; set; }
    public PlayerConnectionStatus ConnectionStatus { get; set; } = PlayerConnectionStatus.Connected;
    public bool IsEliminated { get; set; }

    /// <summary>Lobide: ücretli odalarda PaymentInvoice.Status == Confirmed olduğunda true. Practice'te katılır katılmaz true.</summary>
    public bool IsPaymentConfirmed { get; set; }

    /// <summary>Bağlantı ne zaman koptu — AbandonmentTimeoutSeconds sayacı için. Bağlıyken null.</summary>
    public DateTime? DisconnectedAtUtc { get; set; }

    public DateTime LastActionAtUtc { get; set; }

    /// <summary>docs/03-game-rules.md Bölüm 11: PlayerActionRateLimitPerSecond guard'ı için kayan pencere sayacı.</summary>
    public int ActionCountInWindow { get; set; }
    public DateTime ActionWindowStartUtc { get; set; }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 11 "Multi-accounting / self-play": odaya giriş
    /// isteğinin geldiği IP adresi — aynı odada aynı IP'den 2+ katılım olursa
    /// engellenmez, yalnızca bir uyarı loglanır (bkz. RoomEntryService).
    /// </summary>
    public string? JoinIpAddress { get; set; }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 7 (DÜZELTME): bot her zaman şeffaf işaretlenir —
    /// arayüzde "Bot" rozeti bu alandan türer, hiçbir yerde gizlenmez. Bot bir
    /// Wallet/PaymentInvoice'a hiç sahip olmaz (bkz. BotMatchService), bu yüzden
    /// PayoutService havuz hesabında (IsPaymentConfirmed && !IsBot) ile hariç
    /// tutulur — gerçek para akışına hiç girmez.
    /// </summary>
    public bool IsBot { get; set; }

    /// <summary>Yalnızca IsBot=true iken anlamlıdır — bkz. BotDifficulty.</summary>
    public BotDifficulty? BotDifficulty { get; set; }
}
