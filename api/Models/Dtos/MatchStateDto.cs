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

/// <summary>
/// docs/15-asker-hareketi-performans.md Bölüm 6.2: DepartedAtUtc/ArrivesAtUtc mutlak
/// zaman damgaları olarak gönderilir ki client ara kareleri (requestAnimationFrame ile)
/// kendisi hesaplayabilsin — sunucu her frame için ayrı bir pozisyon göndermez, ve
/// yeniden bağlanan bir client da (Bölüm 6.2 resync) bu alanlardan devam edebilir.
/// </summary>
public class ArmyDto
{
    public required string Id { get; init; }
    public required string OwnerId { get; init; }
    public required int SoldierCount { get; init; }
    public required string FromRegionId { get; init; }
    public required string ToRegionId { get; init; }
    public required DateTime DepartedAtUtc { get; init; }
    public required DateTime ArrivesAtUtc { get; init; }
}

/// <summary>docs/15-asker-hareketi-performans.md Bölüm 6.3: yeni bir sevkiyat başladığında anlık yayınlanır.</summary>
public class ArmyDepartedDto
{
    public required ArmyDto Army { get; init; }
}

/// <summary>
/// Bölüm 4/6.3: iki sevkiyat karşılaştığında yayınlanır. WinningArmyId null ise
/// (SurvivorCount == 0) her iki ordu da tamamen elenmiştir.
/// </summary>
public class ArmyClashedDto
{
    public required string FirstArmyId { get; init; }
    public required string SecondArmyId { get; init; }
    public string? WinningArmyId { get; init; }
    public required int SurvivorCount { get; init; }
    public required DateTime ClashAtUtc { get; init; }
}

/// <summary>Bölüm 6.3: bir sevkiyat hedefine ulaştığında yayınlanır (mevcut savaş/ele geçirme sonucu ayrıca MatchState üzerinden gelir).</summary>
public class ArmyArrivedDto
{
    public required string ArmyId { get; init; }
    public required string OwnerId { get; init; }
    public required int SoldierCount { get; init; }
    public required string RegionId { get; init; }
}
