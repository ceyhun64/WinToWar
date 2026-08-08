using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

/// <summary>
/// docs/09-eksik-tarama-promptu.md denetimi (Faz 8): MatchStateMapper'ın Fog of War
/// filtrelemesi — docs/02-architecture.md "Sunucu otoriter olmalı" (gizlenen veri
/// DTO'ya hiç konmaz) — Stage-1 denetiminde test kapsamı eksikliği olarak bulunmuştu.
/// </summary>
public class MatchStateMapperTests
{
    private readonly MapProvider _mapProvider = new(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);

    private Match CreateMatch(bool fogOfWar, MatchStatus status, Player owner)
    {
        var match = new Match
        {
            Id = "m1",
            Room = new Room
            {
                Id = "r1",
                Type = RoomType.Vip,
                MaxPlayers = 2,
                GreyRegionDefenseCount = 1,
                FogOfWar = fogOfWar,
                EntryFeeUsd = 1.00m,
                CreatorPlayerId = owner.Id
            },
            Status = status
        };
        match.Players.Add(owner);

        // luxembourg-city'nin komşuları: esch-sur-alzette, steinfort, ettelbruck (bkz. Data/map.json).
        // "grevenmacher" bu üçünden hiçbirine doğrudan komşu değildir — görünmez kalması beklenir.
        match.Regions["luxembourg-city"] = new Region { Id = "luxembourg-city", OriginalOwnerId = owner.Id, OwnerId = owner.Id, SoldierCount = 5 };
        match.Regions["esch-sur-alzette"] = new Region { Id = "esch-sur-alzette", OriginalOwnerId = null, OwnerId = null, SoldierCount = 1 };
        match.Regions["grevenmacher"] = new Region { Id = "grevenmacher", OriginalOwnerId = null, OwnerId = null, SoldierCount = 3 };

        return match;
    }

    [Fact]
    public void ToDto_FogOfWarOff_AllRegionsVisibleRegardlessOfOwnership()
    {
        var owner = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = CreateMatch(fogOfWar: false, MatchStatus.Playing, owner);

        var dto = MatchStateMapper.ToDto(match, DateTime.UtcNow, _mapProvider, owner.Id);

        Assert.All(dto.Regions, r => Assert.True(r.IsVisible));
        var grevenmacher = dto.Regions.Single(r => r.Id == "grevenmacher");
        Assert.Equal(3, grevenmacher.SoldierCount);
    }

    [Fact]
    public void ToDto_FogOfWarOnAndPlaying_HidesRegionsBeyondOwnedAndNeighboring()
    {
        var owner = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = CreateMatch(fogOfWar: true, MatchStatus.Playing, owner);

        var dto = MatchStateMapper.ToDto(match, DateTime.UtcNow, _mapProvider, owner.Id);

        var home = dto.Regions.Single(r => r.Id == "luxembourg-city");
        Assert.True(home.IsVisible);
        Assert.Equal(owner.Id, home.OwnerId);

        var neighbor = dto.Regions.Single(r => r.Id == "esch-sur-alzette");
        Assert.True(neighbor.IsVisible);

        // Sunucu otoriter olmalı: gizli bölgenin sahip/asker bilgisi DTO'ya hiç konmaz,
        // yalnızca client tarafı bir maskeleme değildir.
        var hidden = dto.Regions.Single(r => r.Id == "grevenmacher");
        Assert.False(hidden.IsVisible);
        Assert.Null(hidden.OwnerId);
        Assert.Null(hidden.OriginalOwnerId);
        Assert.Equal(0, hidden.SoldierCount);
    }

    [Fact]
    public void ToDto_FogOfWarOnButNotPlaying_AllRegionsVisible()
    {
        var owner = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = CreateMatch(fogOfWar: true, MatchStatus.Lobby, owner);

        var dto = MatchStateMapper.ToDto(match, DateTime.UtcNow, _mapProvider, owner.Id);

        Assert.All(dto.Regions, r => Assert.True(r.IsVisible));
    }

    [Fact]
    public void ToDto_NoViewerPlayerIdProvided_AllRegionsVisible()
    {
        var owner = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = CreateMatch(fogOfWar: true, MatchStatus.Playing, owner);

        // /mac/[matchId] snapshot senaryosu: mapProvider/viewerPlayerId hiç verilmez.
        var dto = MatchStateMapper.ToDto(match, DateTime.UtcNow);

        Assert.All(dto.Regions, r => Assert.True(r.IsVisible));
    }

    [Fact]
    public void ToDto_FogOfWarOn_ArmiesOutsideVisibleRegionsAreFilteredOut()
    {
        var owner = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = CreateMatch(fogOfWar: true, MatchStatus.Playing, owner);
        match.Armies.Add(new Army
        {
            Id = "visible-army",
            SequenceNo = 0,
            OwnerId = owner.Id,
            SoldierCount = 2,
            FromRegionId = "luxembourg-city",
            ToRegionId = "esch-sur-alzette",
            DepartedAtUtc = DateTime.UtcNow,
            ArrivesAtUtc = DateTime.UtcNow.AddSeconds(5)
        });
        match.Armies.Add(new Army
        {
            Id = "hidden-army",
            SequenceNo = 1,
            OwnerId = "enemy",
            SoldierCount = 2,
            FromRegionId = "grevenmacher",
            ToRegionId = "grevenmacher",
            DepartedAtUtc = DateTime.UtcNow,
            ArrivesAtUtc = DateTime.UtcNow.AddSeconds(5)
        });

        var dto = MatchStateMapper.ToDto(match, DateTime.UtcNow, _mapProvider, owner.Id);

        Assert.Single(dto.Armies);
        Assert.Equal("visible-army", dto.Armies[0].Id);
    }
}
