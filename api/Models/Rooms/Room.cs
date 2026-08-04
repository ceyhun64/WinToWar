namespace api.Models.Rooms;

public enum RoomType
{
    Standard,
    Vip,
    Practice
}

/// <summary>
/// Bir maçın oda ayarları (docs/03-game-rules.md Bölüm 2). Standart ve Practice
/// odalarda değerler GameConfig'ten sabit atanır; VIP'de kurucu tarafından seçilir
/// (Bölüm 2.2). Match.Room üzerinden erişilir — ayrı bir kalıcı depo yoktur, maçın
/// kendisiyle aynı bellek içi yaşam döngüsünü paylaşır (bkz. MatchManager).
/// </summary>
public class Room
{
    public required string Id { get; init; }
    public required RoomType Type { get; init; }
    public required int MaxPlayers { get; init; }
    public required int GreyRegionDefenseCount { get; init; }
    public required bool FogOfWar { get; init; }
    public required decimal EntryFeeUsd { get; init; }
    public required string CreatorPlayerId { get; init; }

    /// <summary>Doluysa oda şifrelidir (salt+hash) — bkz. Bölüm 2.2 "DÜZELTME".</summary>
    public string? RoomPasswordHash { get; init; }

    /// <summary>Şifreli oda için opsiyonel kısayol linki; parolanın YERİNE geçmez.</summary>
    public string? InviteToken { get; init; }

    public bool IsPractice => Type == RoomType.Practice;
    public bool IsPasswordProtected => RoomPasswordHash is not null;
}
