using api.Models;
using Microsoft.Extensions.Logging;

namespace api.Services.GameEngine;

/// <summary>
/// Saldırı/savunma çözümleme mantığı. Sayısal üstünlük kazanır modeli kullanılır
/// (bkz. Bölüm 4.3): saldıran güç = asker sayısı; savunan güç = asker sayısı +
/// okçu sayısı * GameConfig.ArcherDefenseMultiplier.
///
/// Bu sınıftaki metotlar çağrılırken ilgili Match.Lock tutuluyor olmalıdır
/// (bkz. MatchManager, GameHub, EconomyTickService).
/// </summary>
public class CombatService
{
    private readonly ILogger<CombatService> _logger;

    public CombatService(ILogger<CombatService> logger)
    {
        _logger = logger;
    }

    public void ResolveAttack(Match match, Army army, Region targetRegion, General general)
    {
        var isNeutral = targetRegion.OwnerId is null;
        var defenderSoldiers = isNeutral ? targetRegion.NeutralDefenseSoldiers : targetRegion.Nest!.GarrisonSoldiers;
        var defenderArchers = isNeutral ? 0 : targetRegion.Nest!.GarrisonArchers;
        var defenderPower = defenderSoldiers + defenderArchers * GameConfig.ArcherDefenseMultiplier;
        var attackerPower = army.SoldierCount;

        if (attackerPower > defenderPower)
        {
            CaptureRegion(match, army, targetRegion, general, defenderPower);
        }
        else
        {
            RepelAttack(match, army, targetRegion, general, attackerPower);
        }
    }

    private void CaptureRegion(Match match, Army army, Region targetRegion, General general, double defenderPower)
    {
        var previousOwnerId = targetRegion.OwnerId;
        var survivingSoldiers = army.SoldierCount - (int)defenderPower;

        targetRegion.OwnerId = army.OwnerId;
        targetRegion.NeutralDefenseSoldiers = 0;
        targetRegion.Nest = new Nest
        {
            RegionId = targetRegion.Id,
            OwnerId = army.OwnerId,
            Level = 1,
            GarrisonSoldiers = survivingSoldiers,
            GarrisonArchers = 0
        };

        general.Status = GeneralStatus.Garrisoned;
        general.CurrentRegionId = targetRegion.Id;

        _logger.LogInformation(
            "Bölge ele geçirildi: {RegionId}, yeni sahip {OwnerId}, kalan asker {Soldiers}",
            targetRegion.Id, army.OwnerId, survivingSoldiers);

        if (previousOwnerId is not null)
        {
            var previousOwner = match.Players.FirstOrDefault(p => p.Id == previousOwnerId);
            if (previousOwner is not null && !match.Regions.Values.Any(r => r.OwnerId == previousOwnerId && r.Nest is not null))
            {
                previousOwner.IsEliminated = true;
                _logger.LogInformation("Oyuncu elendi: {PlayerId}", previousOwnerId);
            }
        }
    }

    private void RepelAttack(Match match, Army army, Region targetRegion, General general, double attackerPower)
    {
        general.Status = GeneralStatus.Dead;
        general.CurrentRegionId = null;
        general.RespawnAtUtc = DateTime.UtcNow.AddSeconds(GameConfig.GeneralRespawnTimeSeconds);

        if (targetRegion.OwnerId is null)
        {
            var soldiersLost = Math.Min(targetRegion.NeutralDefenseSoldiers, (int)attackerPower);
            targetRegion.NeutralDefenseSoldiers -= soldiersLost;
        }
        else
        {
            var nest = targetRegion.Nest!;
            var soldiersLost = Math.Min(nest.GarrisonSoldiers, (int)attackerPower);
            nest.GarrisonSoldiers -= soldiersLost;
            var remainingDamage = attackerPower - soldiersLost;
            if (remainingDamage > 0)
            {
                var archersLost = Math.Min(nest.GarrisonArchers, (int)(remainingDamage / GameConfig.ArcherDefenseMultiplier));
                nest.GarrisonArchers -= archersLost;
            }
        }

        _logger.LogInformation(
            "Saldırı püskürtüldü: {RegionId}, saldıran General {GeneralId} öldü", targetRegion.Id, general.Id);
    }
}
