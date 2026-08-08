namespace api.Models.Dtos;

/// <summary>
/// SignalR üzerinden client'a gönderilen maç durumu. Backend domain modeli
/// (Match, Player, Region, ...) doğrudan yayınlanmaz; her zaman bu DTO'lara
/// map'lenir. web/lib/game/types.ts içindeki TypeScript tipleri bunlarla
/// birebir eşleşmelidir.
/// </summary>
public class MatchStateDto
{
    public required string MatchId { get; init; }
    public required string Status { get; init; }
    public required RoomDto Room { get; init; }
    public required int LobbyConfirmedCount { get; init; }
    public int? CountdownRemainingSeconds { get; init; }
    public required List<string> Winners { get; init; }
    public required List<PlayerDto> Players { get; init; }
    public required List<RegionStateDto> Regions { get; init; }
    public required List<ArmyDto> Armies { get; init; }

    /// <summary>docs/07-pages.md `/mac/[matchId]`: maç süresini göstermek için.</summary>
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}

public class RoomDto
{
    public required string Type { get; init; }
    public required int MaxPlayers { get; init; }
    public required int GreyRegionDefenseCount { get; init; }
    public required bool FogOfWar { get; init; }
    public required string EntryFeeUsd { get; init; }
    public required bool IsPasswordProtected { get; init; }

    /// <summary>Standart/Practice'te boş string — yalnızca VIP'de anlamlıdır (bkz. GameHub.StartVipMatchNow).</summary>
    public required string CreatorPlayerId { get; init; }
}

public class PlayerDto
{
    public required string Id { get; init; }
    public required int Slot { get; init; }
    public required string Name { get; init; }
    public required bool IsEliminated { get; init; }
    public required bool IsConnected { get; init; }
    public required bool IsPaymentConfirmed { get; init; }

    /// <summary>docs/03-game-rules.md Bölüm 7: her zaman şeffaf gösterilir, gizlenmez.</summary>
    public required bool IsBot { get; init; }
}

public class RegionStateDto
{
    public required string Id { get; init; }
    public string? OriginalOwnerId { get; init; }
    public string? OwnerId { get; init; }
    public required int SoldierCount { get; init; }

    /// <summary>
    /// docs/04-style.md Bölüm 10 "Fog of War": false ise yalnızca arazi şekli
    /// görünür, sahip/asker bilgisi sunucu tarafında zaten gizlenmiştir (OwnerId=null,
    /// SoldierCount=0) — client bunları göstermez, yalnızca "keşfedilmemiş alan"
    /// dolgusunu render eder. Room.FogOfWar=false olan odalarda her zaman true'dur.
    /// </summary>
    public required bool IsVisible { get; init; }
}

public class ArmyDto
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required int SoldierCount { get; init; }
    public required string FromRegionId { get; init; }
    public required string ToRegionId { get; init; }
    public required int ArrivesInSeconds { get; init; }
}
