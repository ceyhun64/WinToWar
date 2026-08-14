using api;
using api.Models;
using api.Models.Rooms;
using api.Services;
using api.Services.GameEngine;
using api.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests;

public class MovementServiceTests
{
    private readonly MapProvider _mapProvider = new(new FakeHostEnvironment(), NullLogger<MapProvider>.Instance);
    private readonly MovementService _sut;

    public MovementServiceTests()
    {
        var combatService = new CombatService(TestEventLog.Writer(), NullLogger<CombatService>.Instance);
        _sut = new MovementService(_mapProvider, combatService, NullLogger<MovementService>.Instance);
    }

    private static (Match match, Player player, Region region) CreateOwnedRegion(string regionId, int soldiers)
    {
        var player = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var match = new Match
        {
            Id = "m1",
            Room = new Room
            {
                Id = "r1",
                Type = RoomType.Standard,
                MaxPlayers = 4,
                GreyRegionDefenseCount = 1,
                FogOfWar = false,
                EntryFeeUsd = 1.00m,
                CreatorPlayerId = "creator"
            }
        };
        match.Players.Add(player);

        var region = new Region { Id = regionId, OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = soldiers };
        match.Regions[region.Id] = region;

        return (match, player, region);
    }

    /// <summary>
    /// docs/19-army.md: bir dispatch'i tamamen tüketir — ilk grup StartDispatch'te anında
    /// (§4 "T0 → asker 1"), kalan tamamı tek bir "yeterince ileri zaman" ProcessDispatches
    /// çağrısında (§29 elapsed-time formülü: hangi anda sorulursa sorulsun, o ana kadar
    /// olması gereken toplam aynı kalır — frame/tick sayımına bağlı değildir).
    /// </summary>
    private (Dispatch Dispatch, List<ArmyDepartureResult> Batches, int TotalSent) DrainFully(
        Match match, Player player, Region region, string toRegionId, DateTime start)
    {
        var startResult = _sut.StartDispatch(match, player, region, toRegionId, start);
        var batches = new List<ArmyDepartureResult>();
        if (startResult.FirstBatch is not null)
        {
            batches.Add(startResult.FirstBatch);
        }

        if (match.Dispatches.Contains(startResult.Dispatch))
        {
            batches.AddRange(_sut.ProcessDispatches(match, start.AddSeconds(10)));
        }

        return (startResult.Dispatch, batches, batches.Sum(b => b.DepartedArmy.SoldierCount));
    }

    [Fact]
    public void StartDispatch_Success_ReservesFullAvailableAmountAndSpawnsFirstBatchImmediately()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var now = DateTime.UtcNow;

        var result = _sut.StartDispatch(match, player, region, "esch-sur-alzette", now);

        Assert.Equal(10, result.Dispatch.TotalAmount);
        Assert.NotNull(result.FirstBatch);
        Assert.InRange(result.FirstBatch!.DepartedArmy.SoldierCount, 1, 10);
        Assert.Equal("esch-sur-alzette", result.FirstBatch.DepartedArmy.ToRegionId);
        Assert.Equal(10 - result.FirstBatch.DepartedArmy.SoldierCount, region.SoldierCount);
        Assert.Equal(now.AddSeconds(GameConfig.MovementDurationSeconds), result.FirstBatch.DepartedArmy.ArrivesAtUtc);
        Assert.Null(result.FirstBatch.Clash);
    }

    /// <summary>docs/19-army.md §4/§21: askerler kademeli çıkmalı — region.SoldierCount tek seferde değil, gerçek zamana yayılı azalmalı.</summary>
    [Fact]
    public void StartDispatch_ImmediateFirstBatch_DoesNotDrainMoreThanASingleBatchWorth()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 20);
        var now = DateTime.UtcNow;

        var result = _sut.StartDispatch(match, player, region, "esch-sur-alzette", now);

        // 20 asker, docs/19-army.md §18 ölçekleme tablosuna göre birden fazla grupta
        // ayrılır (§4 "çok hızlı olursa askerler yine tek blok gibi görünür") — anında
        // ayrılan ilk grup, toplamın TAMAMINDAN kesinlikle azdır.
        Assert.True(result.FirstBatch!.DepartedArmy.SoldierCount < 20);
        Assert.True(region.SoldierCount > 0, "Kademeli çıkış: ilk anda kaynakta hâlâ asker kalmalı.");
    }

    [Fact]
    public void StartDispatch_FullyDrained_SendsExactlyTheAvailableAmountNeverMoreNeverLess()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var now = DateTime.UtcNow;

        var (_, _, totalSent) = DrainFully(match, player, region, "esch-sur-alzette", now);

        Assert.Equal(10, totalSent);
        Assert.Equal(0, region.SoldierCount);
        Assert.Empty(match.Dispatches);
        Assert.Equal(10, match.Armies.Sum(a => a.SoldierCount));
    }

    [Fact]
    public void StartDispatch_NotNeighbor_StillSucceeds()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);

        var result = _sut.StartDispatch(match, player, region, "remich", DateTime.UtcNow);

        Assert.Equal("remich", result.Dispatch.ToRegionId);
    }

    [Fact]
    public void StartDispatch_NoSoldiersAvailable_Throws()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 0);

        Assert.Throws<InvalidOperationException>(() =>
            _sut.StartDispatch(match, player, region, "esch-sur-alzette", DateTime.UtcNow));
    }

    [Fact]
    public void StartDispatch_NotOwner_Throws()
    {
        var (match, _, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var other = new Player { Id = "p2", Slot = 1, Name = "Bob" };

        Assert.Throws<InvalidOperationException>(() =>
            _sut.StartDispatch(match, other, region, "esch-sur-alzette", DateTime.UtcNow));
    }

    /// <summary>
    /// docs/19-army.md §10 "ÇİFT HARCAMA YASAK" + §31 Test D/H: aynı source'tan, ilk
    /// sevkiyat henüz tamamen ayrılmamışken (rezerve ettiği pay hâlâ dursa bile) çok kısa
    /// süre sonra (elapsed=0) gelen ikinci bir saldırı, ilk sevkiyatın henüz ayrılmamış
    /// payını KULLANAMAZ — iki dispatch'e birden taahhüt edilen toplam, kaynağın gerçek
    /// orijinal toplamını (20) hiçbir zaman aşamaz.
    /// </summary>
    [Fact]
    public void StartDispatch_SecondCallImmediatelyAfterFirst_NeverExceedsOriginalTotal()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 20);
        var now = DateTime.UtcNow;

        var first = _sut.StartDispatch(match, player, region, "esch-sur-alzette", now);
        var reservedByFirst = first.Dispatch.TotalAmount - first.Dispatch.SpawnedCount;
        var availableForSecond = region.SoldierCount - reservedByFirst;

        if (availableForSecond <= 0)
        {
            Assert.Throws<InvalidOperationException>(() =>
                _sut.StartDispatch(match, player, region, "steinfort", now));
        }
        else
        {
            var second = _sut.StartDispatch(match, player, region, "steinfort", now);
            Assert.Equal(availableForSecond, second.Dispatch.TotalAmount);
        }

        var totalCommitted = match.Dispatches.Sum(d => d.TotalAmount - d.SpawnedCount) + match.Armies.Sum(a => a.SoldierCount);
        Assert.True(totalCommitted <= 20, $"Toplam taahhüt ({totalCommitted}) orijinal kaynağı (20) aşamaz.");
    }

    /// <summary>docs/19-army.md §27: bir source'tan aynı anda birden fazla aktif dispatch (farklı hedeflere) desteklenmelidir.</summary>
    [Fact]
    public void StartDispatch_MultipleActiveDispatchesFromSameSource_ToDifferentTargets_AreTrackedIndependently()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 20);
        var now = DateTime.UtcNow;

        var first = _sut.StartDispatch(match, player, region, "esch-sur-alzette", now);
        // İlk dispatch her şeyi rezerve etmiş olsa da (kalanı hâlâ 0'dan büyükse) ikinci
        // bir hedefe deneme yapılabilmeli — burada asıl kontrol edilen: iki dispatch aynı
        // anda match.Dispatches içinde bağımsız olarak var olabiliyor mu (crash/kilitlenme yok).
        Assert.Contains(first.Dispatch, match.Dispatches.Where(d => d.ToRegionId == "esch-sur-alzette"));

        region.SoldierCount += 5; // üretim simülasyonu: yeni asker geldi.
        var second = _sut.StartDispatch(match, player, region, "steinfort", now);

        Assert.Equal(5, second.Dispatch.TotalAmount);
        Assert.Equal("steinfort", second.Dispatch.ToRegionId);
        Assert.Contains(match.Dispatches, d => d.ToRegionId == "esch-sur-alzette");
        Assert.Contains(match.Dispatches, d => d.ToRegionId == "steinfort");
    }

    /// <summary>docs/19-army.md §31 Test G: "20 → A + production" — dispatch tüketildikten sonra gelen üretim, kaynağın gerçek asker sayısını normal şekilde artırmaya devam eder ve yeni bir dispatch'e konu olabilir.</summary>
    [Fact]
    public void StartDispatch_AfterFirstDispatchFullyDrains_NewProductionBecomesAvailableForSecondDispatch()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 5);
        var now = DateTime.UtcNow;

        DrainFully(match, player, region, "esch-sur-alzette", now);
        Assert.Equal(0, region.SoldierCount);

        region.SoldierCount = 3; // EconomyTickService.ApplyProduction'ın sorumluluğu, burada simüle edildi.
        var second = _sut.StartDispatch(match, player, region, "steinfort", now.AddSeconds(2));

        Assert.Equal(3, second.Dispatch.TotalAmount);
    }

    [Fact]
    public void StartDispatch_MultipleCalls_AssignsStrictlyIncreasingSequenceNo()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var now = DateTime.UtcNow;

        var first = _sut.StartDispatch(match, player, region, "esch-sur-alzette", now);
        region.SoldierCount = 10; // ikinci gönderim için yeniden doldur (production simülasyonu).
        var second = _sut.StartDispatch(match, player, region, "steinfort", now);

        Assert.True(second.Dispatch.SequenceNo > first.Dispatch.SequenceNo);
    }

    /// <summary>docs/19-army.md §22: bir dispatch'in bütün askerleri kaynaktan çıktığında artık aktif sayılmamalı ve kaynak o askerleri tekrar kullanamamalı.</summary>
    [Fact]
    public void ProcessDispatches_DispatchFullyDrained_IsRemovedFromActiveList()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 3);
        var now = DateTime.UtcNow;

        var start = _sut.StartDispatch(match, player, region, "esch-sur-alzette", now);
        _sut.ProcessDispatches(match, now.AddSeconds(10));

        Assert.DoesNotContain(start.Dispatch, match.Dispatches);
    }

    /// <summary>docs/19-army.md: kaynak bölge dispatch devam ederken başka bir saldırıyla el değiştirirse, henüz ayrılmamış pay geçersiz sayılır (kaynak zaten yeni sahibin/nötrün asker sayısıyla sıfırlanmıştır).</summary>
    [Fact]
    public void ProcessDispatches_SourceRegionCapturedMidDispatch_CancelsRemainingUnspawnedPortion()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 20);
        var now = DateTime.UtcNow;

        var start = _sut.StartDispatch(match, player, region, "esch-sur-alzette", now);
        Assert.True(start.Dispatch.SpawnedCount < start.Dispatch.TotalAmount);

        // Bölge başka bir oyuncu tarafından ele geçirildi (CombatService'in normal sonucu).
        region.OwnerId = "p2";
        region.SoldierCount = 1;

        var results = _sut.ProcessDispatches(match, now.AddSeconds(10));

        Assert.Empty(results);
        Assert.DoesNotContain(start.Dispatch, match.Dispatches);
        Assert.Equal(1, region.SoldierCount); // yeni sahibin askerine dokunulmadı.
    }

    [Fact]
    public void ProcessArrivals_SimultaneousArrivalsAtSameRegion_ProcessedInSequenceOrderNotByRandomId()
    {
        var (match, player, _) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var target = new Region { Id = "esch-sur-alzette", OriginalOwnerId = null, OwnerId = null, SoldierCount = 100 };
        match.Regions[target.Id] = target;
        var now = DateTime.UtcNow;

        var firstDeparted = new Army
        {
            Id = "z-departed-first",
            SequenceNo = 0,
            OwnerId = player.Id,
            SoldierCount = 3,
            FromRegionId = "x",
            ToRegionId = target.Id,
            DepartedAtUtc = now,
            ArrivesAtUtc = now
        };
        var secondDeparted = new Army
        {
            Id = "a-departed-second",
            SequenceNo = 1,
            OwnerId = player.Id,
            SoldierCount = 3,
            FromRegionId = "y",
            ToRegionId = target.Id,
            DepartedAtUtc = now,
            ArrivesAtUtc = now
        };
        match.Armies.Add(firstDeparted);
        match.Armies.Add(secondDeparted);

        var arrived = _sut.ProcessArrivals(match, now);

        Assert.Equal(2, arrived.Count);
        Assert.Equal(firstDeparted.Id, arrived[0].Id);
        Assert.Equal(secondDeparted.Id, arrived[1].Id);
    }

    [Fact]
    public void ProcessArrivals_ReinforcementToOwnRegion_MergesGarrisonWithoutCombat()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 10);
        var targetRegion = new Region { Id = "esch-sur-alzette", OriginalOwnerId = player.Id, OwnerId = player.Id, SoldierCount = 1 };
        match.Regions[targetRegion.Id] = targetRegion;

        var now = DateTime.UtcNow;
        DrainFully(match, player, region, "esch-sur-alzette", now);
        foreach (var army in match.Armies)
        {
            army.ArrivesAtUtc = now.AddSeconds(-1);
        }

        var arrived = _sut.ProcessArrivals(match, DateTime.UtcNow);

        Assert.NotEmpty(arrived);
        Assert.Equal(1 + 10, targetRegion.SoldierCount);
        Assert.Empty(match.Armies);
    }

    [Fact]
    public void ProcessArrivals_ToEnemyRegion_TriggersCombatCaptureAndAllSurvivorsBecomeGarrison()
    {
        var (match, player, region) = CreateOwnedRegion("luxembourg-city", soldiers: 5);
        var enemy = new Player { Id = "p2", Slot = 1, Name = "Bob" };
        match.Players.Add(enemy);
        var enemyRegion = new Region { Id = "esch-sur-alzette", OriginalOwnerId = enemy.Id, OwnerId = enemy.Id, SoldierCount = 1 };
        match.Regions[enemyRegion.Id] = enemyRegion;

        var now = DateTime.UtcNow;
        DrainFully(match, player, region, "esch-sur-alzette", now);
        foreach (var army in match.Armies)
        {
            army.ArrivesAtUtc = now.AddSeconds(-1);
        }

        _sut.ProcessArrivals(match, DateTime.UtcNow);

        // 5 askerin TAMAMI (docs/19-army.md kademeli gruplar halinde de olsa) nihayetinde
        // gönderilir; sıralı çözümleme (docs/03-game-rules.md Bölüm 10) sayesinde toplam
        // sonuç, tek seferlik gönderimle birebir aynıdır: savunma (1) yenilir, kalan 4
        // asker garrison olarak kalır.
        Assert.Equal(player.Id, enemyRegion.OwnerId);
        Assert.Equal(4, enemyRegion.SoldierCount);
    }

    /// <summary>
    /// docs/15-asker-hareketi-performans.md Bölüm 4.2 + Bölüm 9 kabul kriteri: müşterinin
    /// birebir verdiği örnek — "20 ile 10 karşılaştı, 20 gelen taraftan 10 tanesi diğer
    /// tarafa devam edecek". Çarpışma FORMÜLÜ docs/19-army.md ile değişmedi (§24 "combat
    /// formülünü değiştirme") — burada tek bir Army nesnesi üzerinden (StartDispatch'in
    /// kademeli gruplamasından bağımsız) doğrudan test edilir, tıpkı üstteki
    /// ProcessArrivals_SimultaneousArrivalsAtSameRegion testindeki gibi.
    /// </summary>
    [Fact]
    public void CreateArmyBatch_OpposingArmyInTransit_TwentyVsTen_WinnerContinuesWithDifference()
    {
        var match = new Match
        {
            Id = "m1",
            Room = new Room
            {
                Id = "r1",
                Type = RoomType.Standard,
                MaxPlayers = 4,
                GreyRegionDefenseCount = 1,
                FogOfWar = false,
                EntryFeeUsd = 1.00m,
                CreatorPlayerId = "creator"
            }
        };
        var p1 = new Player { Id = "p1", Slot = 0, Name = "Alice" };
        var p2 = new Player { Id = "p2", Slot = 1, Name = "Bob" };
        match.Players.Add(p1);
        match.Players.Add(p2);
        var regionA = new Region { Id = "luxembourg-city", OriginalOwnerId = p1.Id, OwnerId = p1.Id, SoldierCount = 20 };
        var regionB = new Region { Id = "esch-sur-alzette", OriginalOwnerId = p2.Id, OwnerId = p2.Id, SoldierCount = 10 };
        match.Regions[regionA.Id] = regionA;
        match.Regions[regionB.Id] = regionB;
        var t0 = DateTime.UtcNow;

        // docs/19-army.md'nin kademeli batch mantığından bağımsız olarak, doğrudan tek
        // bir Army nesnesi enjekte edilerek (yukarıdaki emsal test ile aynı üslup)
        // ResolveClash formülü izole test edilir.
        var twenty = new Army
        {
            Id = "twenty", SequenceNo = 0, OwnerId = p1.Id, SoldierCount = 20,
            FromRegionId = regionA.Id, ToRegionId = regionB.Id, DepartedAtUtc = t0,
            ArrivesAtUtc = t0.AddSeconds(GameConfig.MovementDurationSeconds)
        };
        match.Armies.Add(twenty);
        regionA.SoldierCount = 0;

        var t1 = t0.AddSeconds(1);
        var result = _sut.StartDispatch(match, p2, regionB, regionA.Id, t1);
        // regionB=10 asker, tek grupta (batchCount clamp'i totalAmount=10'a göre 8 grup
        // üretse de) StartDispatch'in ilk batch'i tüm 10'u temsil etmeyebilir — bu testin
        // amacı yalnızca "10"un TAMAMI tek seferde yola çıkarsa" davranışını izole
        // doğrulamak olduğundan, kalanını da hemen tükettiriyoruz.
        var remaining = _sut.ProcessDispatches(match, t1.AddSeconds(10));
        var allTenBatches = new List<ArmyDepartureResult>();
        if (result.FirstBatch is not null) allTenBatches.Add(result.FirstBatch);
        allTenBatches.AddRange(remaining);

        // İlk batch (veya biri) opposing "twenty" ordusuyla çarpışmış olmalı.
        var clashed = allTenBatches.FirstOrDefault(b => b.Clash is not null);
        Assert.NotNull(clashed);
        Assert.Equal(twenty.Id, clashed!.Clash!.WinningArmyId);

        var survivingArmy = Assert.Single(match.Armies);
        Assert.Equal(twenty.Id, survivingArmy.Id);
        Assert.Equal(10, survivingArmy.SoldierCount);
        Assert.Equal(regionB.Id, survivingArmy.ToRegionId);
    }
}
