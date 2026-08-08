using System.Globalization;
using api.Models;
using api.Models.Dtos;

namespace api.Services;

/// <summary>
/// Match (domain) -> MatchStateDto (client'a giden DTO) dönüşümü. Backend domain
/// modeli SignalR üzerinden asla doğrudan yayınlanmaz.
/// </summary>
public static class MatchStateMapper
{
    /// <summary>
    /// <paramref name="mapProvider"/>/<paramref name="viewerPlayerId"/> yalnızca
    /// Room.FogOfWar=true ve Match.Status=Playing olduğunda kullanılır (docs/04-style.md
    /// Bölüm 10, docs/02-architecture.md "Sunucu otoriter olmalı"): viewer'ın kendi
    /// bölgeleri ve bunlara doğrudan komşu bölgeler dışındaki bölgelerin sahip/asker
    /// bilgisi DTO'ya hiç konmaz (client'a gönderilmeden gizlenir, yalnızca client
    /// tarafı bir maskeleme değildir). Bu iki parametre verilmezse (ör. lobi/sonuç
    /// ekranı, veya fog kapalı bir oda) tüm bölgeler her zaman görünür kabul edilir.
    /// </summary>
    public static MatchStateDto ToDto(Match match, DateTime now, MapProvider? mapProvider = null, string? viewerPlayerId = null)
    {
        var visibleRegionIds = ComputeVisibleRegionIds(match, mapProvider, viewerPlayerId);

        return new MatchStateDto
        {
            MatchId = match.Id,
            Status = match.Status.ToString(),
            Room = new RoomDto
            {
                Type = match.Room.Type.ToString(),
                MaxPlayers = match.Room.MaxPlayers,
                GreyRegionDefenseCount = match.Room.GreyRegionDefenseCount,
                FogOfWar = match.Room.FogOfWar,
                EntryFeeUsd = match.Room.EntryFeeUsd.ToString(CultureInfo.InvariantCulture),
                IsPasswordProtected = match.Room.IsPasswordProtected,
                CreatorPlayerId = match.Room.CreatorPlayerId
            },
            LobbyConfirmedCount = match.Players.Count(p => p.IsPaymentConfirmed),
            CountdownRemainingSeconds = match.CountdownEndsAtUtc is DateTime countdownEndsAt
                ? Math.Max(0, (int)(countdownEndsAt - now).TotalSeconds)
                : null,
            Winners = match.Winners.ToList(),
            Players = match.Players.Select(p => new PlayerDto
            {
                Id = p.Id,
                Slot = p.Slot,
                Name = p.Name,
                IsEliminated = p.IsEliminated,
                IsConnected = p.ConnectionStatus == PlayerConnectionStatus.Connected,
                IsPaymentConfirmed = p.IsPaymentConfirmed,
                IsBot = p.IsBot
            }).ToList(),
            Regions = match.Regions.Values.Select(r =>
            {
                var isVisible = visibleRegionIds is null || visibleRegionIds.Contains(r.Id);
                return new RegionStateDto
                {
                    Id = r.Id,
                    OriginalOwnerId = isVisible ? r.OriginalOwnerId : null,
                    OwnerId = isVisible ? r.OwnerId : null,
                    SoldierCount = isVisible ? r.SoldierCount : 0,
                    IsVisible = isVisible
                };
            }).ToList(),
            Armies = match.Armies
                .Where(a => visibleRegionIds is null || visibleRegionIds.Contains(a.FromRegionId) || visibleRegionIds.Contains(a.ToRegionId))
                .Select(a => new ArmyDto
                {
                    Id = a.Id,
                    OwnerId = a.OwnerId,
                    SoldierCount = a.SoldierCount,
                    FromRegionId = a.FromRegionId,
                    ToRegionId = a.ToRegionId,
                    ArrivesInSeconds = Math.Max(0, (int)(a.ArrivesAtUtc - now).TotalSeconds)
                }).ToList(),
            StartedAtUtc = match.StartedAtUtc,
            CompletedAtUtc = match.CompletedAtUtc
        };
    }

    /// <summary>
    /// Fog kapalıysa, oyun henüz Playing değilse veya viewer bilinmiyorsa (ör. lobi
    /// yayını, `/mac/[matchId]` snapshot'ı) null döner — bu, ToDto'da "tüm bölgeler
    /// görünür" anlamına gelir. Aksi halde viewer'ın sahip olduğu bölgeler + bunlara
    /// doğrudan komşu bölgelerin id kümesini döner.
    /// </summary>
    private static HashSet<string>? ComputeVisibleRegionIds(Match match, MapProvider? mapProvider, string? viewerPlayerId)
    {
        if (mapProvider is null || viewerPlayerId is null || !match.Room.FogOfWar || match.Status != MatchStatus.Playing)
        {
            return null;
        }

        var ownedRegionIds = match.Regions.Values.Where(r => r.OwnerId == viewerPlayerId).Select(r => r.Id).ToList();
        var visible = new HashSet<string>(ownedRegionIds);

        foreach (var regionId in ownedRegionIds)
        {
            if (mapProvider.RegionsById.TryGetValue(regionId, out var definition))
            {
                visible.UnionWith(definition.Neighbors);
            }
        }

        return visible;
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
                NeighborIds = r.Neighbors.ToList()
            }).ToList()
        };
    }
}
