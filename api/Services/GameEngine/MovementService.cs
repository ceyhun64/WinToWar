using api.Models;

namespace api.Services.GameEngine;

/// <summary>
/// docs/15-asker-hareketi-performans.md Bölüm 4: iki ordu karşılaştığında (aynı
/// bölge çifti arasında ters yönlü, aynı anda aktif) hangisinin kazandığı, kaç
/// asker hayatta kaldığı ve görsel karşılaşma anı. WinningArmyId null ise
/// (SurvivorCount == 0) her iki ordu da tamamen elenmiştir (Bölüm 4.2 "eşit sayı").
/// </summary>
public record ArmyClashInfo(string FirstArmyId, string SecondArmyId, string? WinningArmyId, int SurvivorCount, DateTime ClashAtUtc);

/// <summary>Bir Army grubunun (batch) yola çıkışının sonucu: grubun kendisi ve varsa çarpışma bilgisi.</summary>
public record ArmyDepartureResult(Army DepartedArmy, ArmyClashInfo? Clash);

/// <summary>docs/19-army.md: bir Dispatch başlatıldığında, ilk batch'in anında yola çıkma sonucu.</summary>
public record DispatchStartResult(Dispatch Dispatch, ArmyDepartureResult? FirstBatch);

/// <summary>
/// Ordu hareketi. Bölgeler arası hareket anlık değildir; sabit bir süre kullanılır
/// (GameConfig.MovementDurationSeconds). Saldırı artık komşulukla sınırlı değildir —
/// haritadaki herhangi bir bölgeden herhangi bir başka bölgeye doğrudan gönderim
/// yapılabilir (GameConfig.AttackAdjacencyOnly = false, docs/03-game-rules.md Bölüm
/// 3/6/15-D.1 — müşteri kararıyla çözüldü).
///
/// docs/19-army.md: bir gönderim artık anlık değildir. `StartDispatch`, kaynak
/// bölgede o an FİİLEN kullanılabilir asker sayısının (SoldierCount eksi aynı
/// bölgeden çıkan diğer aktif dispatch'lerin henüz ayrılmamış payı — "rezerve"
/// miktar) tamamını yeni bir `Dispatch`'e atar; aynı asker iki dispatch'e birden
/// atanamaz (docs/03-game-rules.md Bölüm 6, Bölüm 9-10). Askerler bu dispatch'ten
/// gerçek geçen zamana göre kademeli gruplar (`Army` kayıtları) halinde
/// `ProcessDispatches` (EconomyTickService'in periyodik tick'i içinden, Match.Lock
/// altında) tarafından ayrılır — "saldırı başladı" ile "asker source'tan çıktı"
/// artık aynı an değildir.
///
/// Bu sınıftaki metotlar çağrılırken ilgili Match.Lock tutuluyor olmalıdır.
/// </summary>
public class MovementService
{
    private readonly MapProvider _mapProvider;
    private readonly CombatService _combatService;
    private readonly ILogger<MovementService> _logger;

    public MovementService(MapProvider mapProvider, CombatService combatService, ILogger<MovementService> logger)
    {
        _mapProvider = mapProvider;
        _combatService = combatService;
        _logger = logger;
    }

    /// <summary>
    /// docs/19-army.md §9-10: "kullanılabilir" miktar, bölgenin güncel asker sayısından,
    /// AYNI bölgeden çıkan diğer aktif dispatch'lerin henüz ayrılmamış (rezerve) payı
    /// düşülerek hesaplanır — bu, aynı askerin iki farklı hedefe birden atanmasını
    /// (çift harcama) sunucu tarafında yapısal olarak imkansız kılar.
    /// </summary>
    public DispatchStartResult StartDispatch(Match match, Player player, Region fromRegion, string toRegionId, DateTime now)
    {
        if (fromRegion.OwnerId != player.Id)
        {
            throw new InvalidOperationException("Bu bölge size ait değil.");
        }

        if (GameConfig.AttackAdjacencyOnly && !_mapProvider.AreNeighbors(fromRegion.Id, toRegionId))
        {
            throw new InvalidOperationException("Saldırı yalnızca doğrudan komşu bölgeye yapılabilir.");
        }

        var reserved = match.Dispatches
            .Where(d => d.FromRegionId == fromRegion.Id)
            .Sum(d => d.TotalAmount - d.SpawnedCount);
        var available = fromRegion.SoldierCount - reserved;
        if (available <= 0)
        {
            throw new InvalidOperationException("Bu bölgede gönderilecek yeterli asker yok.");
        }

        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = player.Id,
            FromRegionId = fromRegion.Id,
            ToRegionId = toRegionId,
            TotalAmount = available,
            SpawnedCount = 0,
            StartedAtUtc = now,
            SequenceNo = match.NextArmySequenceNo++
        };
        match.Dispatches.Add(dispatch);

        _logger.LogInformation(
            "Sevkiyat başlatıldı: {From} -> {To}, toplam {Total} asker, {Batches} grup halinde",
            fromRegion.Id, toRegionId, available, ScaleBatchCount(available));

        // docs/19-army.md §4 örneği ("T0 → asker 1"): ilk grup anında, tick'i beklemeden ayrılır.
        var firstBatch = SpawnDueBatch(match, dispatch, fromRegion, now);
        if (dispatch.SpawnedCount >= dispatch.TotalAmount)
        {
            match.Dispatches.Remove(dispatch);
        }

        return new DispatchStartResult(dispatch, firstBatch);
    }

    /// <summary>
    /// Her aktif dispatch için, gerçek geçen zamana göre kaç asker ayrılmış olması
    /// gerektiğini hesaplar (docs/19-army.md §28-29: elapsed-time bazlı, frame/interval
    /// sayımına güvenmez) ve bu tick'te "vadesi gelen" farkı tek bir yeni Army grubu
    /// olarak kaynaktan gerçekten ayırır. Match.Lock altında (EconomyTickService.Tick
    /// içinden) çağrılmalıdır.
    /// </summary>
    public List<ArmyDepartureResult> ProcessDispatches(Match match, DateTime now)
    {
        var results = new List<ArmyDepartureResult>();
        var completed = new List<Dispatch>();

        foreach (var dispatch in match.Dispatches)
        {
            // docs/19-army.md: kaynak bölge bu dispatch başladıktan sonra başka bir
            // saldırıyla el değiştirdiyse, henüz ayrılmamış pay artık geçersizdir —
            // o bölge zaten yeni sahibinin/nötrün asker sayısıyla sıfırlanmıştır.
            if (!match.Regions.TryGetValue(dispatch.FromRegionId, out var region) || region.OwnerId != dispatch.OwnerId)
            {
                completed.Add(dispatch);
                continue;
            }

            var batch = SpawnDueBatch(match, dispatch, region, now);
            if (batch is not null)
            {
                results.Add(batch);
            }

            if (dispatch.SpawnedCount >= dispatch.TotalAmount)
            {
                completed.Add(dispatch);
            }
        }

        foreach (var dispatch in completed)
        {
            match.Dispatches.Remove(dispatch);
        }

        return results;
    }

    /// <summary>Bu dispatch için vadesi gelmiş (ama henüz ayrılmamış) askerleri tek bir Army grubu olarak kaynaktan ayırır.</summary>
    private ArmyDepartureResult? SpawnDueBatch(Match match, Dispatch dispatch, Region fromRegion, DateTime now)
    {
        var elapsedMs = Math.Max(0, (now - dispatch.StartedAtUtc).TotalMilliseconds);
        var expectedSpawned = ExpectedSpawnedAmount(dispatch.TotalAmount, elapsedMs);
        var delta = expectedSpawned - dispatch.SpawnedCount;
        if (delta <= 0)
        {
            return null;
        }

        // Guard: reservation hesabı bunu normal akışta hiç aşmamalı, ama negatif bir
        // region.SoldierCount asla oluşmasın (06-coding-standards.md Guard).
        delta = Math.Min(delta, fromRegion.SoldierCount);
        if (delta <= 0)
        {
            return null;
        }

        fromRegion.SoldierCount -= delta;
        dispatch.SpawnedCount += delta;

        return CreateArmyBatch(match, dispatch.OwnerId, dispatch.FromRegionId, dispatch.ToRegionId, delta, now);
    }

    /// <summary>
    /// docs/19-army.md §18 ile aynı ölçekleme mantığı: tek tek her askeri ayrı bir
    /// interval'da göndermek yerine (büyük bir orduda gameplay'i ağırlaştırır, §4),
    /// toplam miktara göre kaç GRUP halinde kademeli ayrılacağı ölçeklenir.
    /// </summary>
    private static int ScaleBatchCount(int totalAmount)
    {
        var batches = totalAmount <= GameConfig.DispatchBatchScaleTinyMax ? GameConfig.DispatchBatchScaleTinyBatches
            : totalAmount <= GameConfig.DispatchBatchScaleSmallMax ? GameConfig.DispatchBatchScaleSmallBatches
            : totalAmount <= GameConfig.DispatchBatchScaleMediumMax ? GameConfig.DispatchBatchScaleMediumBatches
            : GameConfig.DispatchBatchScaleLargeBatches;
        return Math.Clamp(batches, 1, Math.Max(1, totalAmount));
    }

    /// <summary>
    /// Gerçek geçen süreye göre (elapsedMs), bu dispatch'ten şu ana kadar toplam kaç
    /// asker ayrılmış OLMASI gerektiğini döner (docs/19-army.md §29: "expectedDispatched
    /// = floor(elapsed / dispatchInterval)" mantığının aynısı, gruplar üzerinden).
    /// </summary>
    private static int ExpectedSpawnedAmount(int totalAmount, double elapsedMs)
    {
        var batchCount = ScaleBatchCount(totalAmount);
        var expectedBatchIndex = (int)Math.Floor(elapsedMs / GameConfig.DispatchBatchIntervalMs) + 1;
        expectedBatchIndex = Math.Clamp(expectedBatchIndex, 0, batchCount);
        return CumulativeBatchAmount(totalAmount, batchCount, expectedBatchIndex);
    }

    /// <summary>İlk `batchIndex` grubun taşıdığı toplam asker sayısı — kalan, mümkün olduğunca eşit dağıtılır (ilk `remainder` grup birer fazla taşır).</summary>
    private static int CumulativeBatchAmount(int totalAmount, int batchCount, int batchIndex)
    {
        if (batchIndex <= 0) return 0;
        if (batchIndex >= batchCount) return totalAmount;

        var baseSize = totalAmount / batchCount;
        var remainder = totalAmount % batchCount;
        return batchIndex <= remainder
            ? batchIndex * (baseSize + 1)
            : remainder * (baseSize + 1) + (batchIndex - remainder) * baseSize;
    }

    /// <summary>
    /// Bir Army grubu oluşturur ve docs/15-asker-hareketi-performans.md Bölüm 4'teki
    /// karşı-yönlü çarpışma tespitini uygular (aynı bölge çifti arasında ters yönlü,
    /// aktif bir ordu varsa). DepartArmy'nin eski tek-seferlik davranışının yerini alan
    /// StartDispatch/ProcessDispatches tarafından, her gerçek batch için çağrılır.
    /// </summary>
    private ArmyDepartureResult CreateArmyBatch(Match match, string ownerId, string fromRegionId, string toRegionId, int soldierCount, DateTime now)
    {
        var army = new Army
        {
            Id = Guid.NewGuid().ToString("N"),
            SequenceNo = match.NextArmySequenceNo++,
            OwnerId = ownerId,
            SoldierCount = soldierCount,
            FromRegionId = fromRegionId,
            ToRegionId = toRegionId,
            DepartedAtUtc = now,
            ArrivesAtUtc = now.AddSeconds(GameConfig.MovementDurationSeconds)
        };
        match.Armies.Add(army);

        _logger.LogInformation(
            "Ordu yola çıktı: {From} -> {To}, asker {Soldiers}, varış {Arrival}",
            fromRegionId, toRegionId, soldierCount, army.ArrivesAtUtc);

        var opposingArmy = match.Armies.FirstOrDefault(a =>
            a.Id != army.Id && a.FromRegionId == toRegionId && a.ToRegionId == fromRegionId);

        if (opposingArmy is null)
        {
            return new ArmyDepartureResult(army, null);
        }

        var clash = ResolveClash(match, army, opposingArmy);
        return new ArmyDepartureResult(army, clash);
    }

    /// <summary>
    /// docs/15-asker-hareketi-performans.md Bölüm 4.2: survivors = |countA - countB|;
    /// fazla asker sayısına sahip taraf kazanır ve orijinal ArrivesAtUtc'sinde hedefine
    /// ulaşmaya devam eder (süre yeniden hesaplanmaz), kaybeden tamamen silinir. Eşit
    /// sayıda ise (survivors == 0) her ikisi de silinir — ayrı bir kural değil, bu
    /// formülün doğal sonucu.
    /// </summary>
    private ArmyClashInfo ResolveClash(Match match, Army first, Army second)
    {
        var firstCount = first.SoldierCount;
        var secondCount = second.SoldierCount;
        var survivors = Math.Abs(firstCount - secondCount);
        var winner = firstCount == secondCount ? null : (firstCount > secondCount ? first : second);
        var clashAtUtc = ComputeClashTimeUtc(first, second);

        match.Armies.Remove(first);
        match.Armies.Remove(second);

        if (winner is not null)
        {
            winner.SoldierCount = survivors;
            match.Armies.Add(winner);
        }

        _logger.LogInformation(
            "Ordular karşılaştı: {First} ({FirstCount} asker) vs {Second} ({SecondCount} asker), hayatta kalan {Survivors}, kazanan {Winner}",
            first.Id, firstCount, second.Id, secondCount, survivors, winner?.Id ?? "(yok)");

        return new ArmyClashInfo(first.Id, second.Id, winner?.Id, survivors, clashAtUtc);
    }

    /// <summary>
    /// docs/15-asker-hareketi-performans.md Bölüm 4.3: iki ordunun aynı sabit hızda
    /// (GameConfig.MovementDurationSeconds) hareket ettiği varsayımıyla zaman-ağırlıklı
    /// orta nokta. A, tA0'da yola çıkıp D sürede ilerler; B, tB0'da ters yönde yola
    /// çıkar. (t-tA0)/D = 1 - (t-tB0)/D denklemi çözülünce t = (D + tA0 + tB0)/2 çıkar;
    /// burada A'dan bu yana geçen süre olarak yeniden ifade edilir. Sonuç yalnızca
    /// görsel amaçlıdır (client'ın karşılaşma noktasını çizmesi için), çarpışmanın
    /// kendisi (hangi ordunun var olduğu) bu hesaptan bağımsız zaten kesinleşmiştir.
    /// </summary>
    private static DateTime ComputeClashTimeUtc(Army a, Army b)
    {
        var durationSeconds = GameConfig.MovementDurationSeconds;
        var bDepartedOffsetSeconds = (b.DepartedAtUtc - a.DepartedAtUtc).TotalSeconds;
        var elapsedFromA = (durationSeconds + bDepartedOffsetSeconds) / 2.0;
        var clamped = Math.Clamp(elapsedFromA, 0, durationSeconds);
        return a.DepartedAtUtc.AddSeconds(clamped);
    }

    /// <summary>
    /// Varan orduları işler: kendi bölgesine varan takviye garnizona katılır, aksi
    /// halde çatışma çözülür ve hayatta kalan tüm saldıranlar (varsa) yeni garrison
    /// olur — otomatik zincirleme bir sonraki bölgeye devam etmez (tek hop). Aynı
    /// bölgeye varan birden fazla ordu, varış sırasına göre (eşit zamanda oluşturulma/
    /// yola çıkma sırası — Army.SequenceNo, bkz. docs/03-game-rules.md Bölüm 10)
    /// tek tek işlenir — hiçbir zaman birleşik güç olarak toplanmaz.
    /// </summary>
    public List<Army> ProcessArrivals(Match match, DateTime now)
    {
        var arrived = match.Armies
            .Where(a => a.ArrivesAtUtc <= now)
            .OrderBy(a => a.ArrivesAtUtc)
            .ThenBy(a => a.SequenceNo)
            .ToList();

        foreach (var army in arrived)
        {
            match.Armies.Remove(army);
            if (!match.Regions.TryGetValue(army.ToRegionId, out var region))
            {
                continue;
            }

            if (region.OwnerId == army.OwnerId)
            {
                region.SoldierCount += army.SoldierCount;
                _logger.LogInformation("Takviye ulaştı: {RegionId}, asker {Soldiers}", region.Id, army.SoldierCount);
                continue;
            }

            var owner = match.Players.FirstOrDefault(p => p.Id == army.OwnerId);
            if (owner is null || owner.IsEliminated)
            {
                continue;
            }

            _combatService.ResolveAttack(match, army, region, now);
        }

        return arrived;
    }
}
