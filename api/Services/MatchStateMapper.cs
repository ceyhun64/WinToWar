using api.Models;
using api.Models.Dtos;

namespace api.Services;

/// <summary>
/// Match (domain) -> MatchStateDto (client'a giden DTO) dönüşümü. Backend domain
/// modeli SignalR üzerinden asla doğrudan yayınlanmaz.
/// </summary>
public static class MatchStateMapper
{
    public static MatchStateDto ToDto(Match match)
    {
        var now = DateTime.UtcNow;
        var remainingSeconds = GameConfig.MatchDurationSeconds;
        if (match.StartedAtUtc is DateTime startedAt)
        {
            var elapsed = (now - startedAt).TotalSeconds;
            remainingSeconds = Math.Max(0, GameConfig.MatchDurationSeconds - (int)elapsed);
        }

        return new MatchStateDto
        {
            MatchId = match.Id,
            Status = match.Status.ToString(),
            RemainingSeconds = remainingSeconds,
            WinnerId = match.WinnerId,
            Players = match.Players.Select(p => new PlayerDto
            {
                Id = p.Id,
                Slot = p.Slot,
                Name = p.Name,
                Gold = (int)Math.Floor(p.Gold),
                IsEliminated = p.IsEliminated,
                IsConnected = p.ConnectionStatus == PlayerConnectionStatus.Connected
            }).ToList(),
            Regions = match.Regions.Values.Select(r => new RegionStateDto
            {
                Id = r.Id,
                OwnerId = r.OwnerId,
                NestLevel = r.Nest?.Level,
                GarrisonSoldiers = r.Nest?.GarrisonSoldiers ?? 0,
                GarrisonArchers = r.Nest?.GarrisonArchers ?? 0,
                NeutralDefenseSoldiers = r.NeutralDefenseSoldiers
            }).ToList(),
            Generals = match.Generals.Select(g => new GeneralDto
            {
                Id = g.Id,
                OwnerId = g.OwnerId,
                Status = g.Status.ToString(),
                CurrentRegionId = g.CurrentRegionId,
                RespawnInSeconds = g.RespawnAtUtc is DateTime respawnAt
                    ? Math.Max(0, (int)(respawnAt - now).TotalSeconds)
                    : null
            }).ToList(),
            Armies = match.Armies.Select(a => new ArmyDto
            {
                Id = a.Id,
                OwnerId = a.OwnerId,
                GeneralId = a.GeneralId,
                SoldierCount = a.SoldierCount,
                FromRegionId = a.FromRegionId,
                ToRegionId = a.ToRegionId,
                ArrivesInSeconds = Math.Max(0, (int)(a.ArrivesAtUtc - now).TotalSeconds)
            }).ToList()
        };
    }

    public static MapDto ToMapDto(MapDefinition map)
    {
        return new MapDto
        {
            Regions = map.Regions.Select(r => new MapRegionDto
            {
                Id = r.Id,
                Name = r.Name,
                X = r.X,
                Y = r.Y,
                NeighborIds = r.Neighbors.Select(n => n.RegionId).ToList()
            }).ToList()
        };
    }
}
