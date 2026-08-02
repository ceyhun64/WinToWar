using api.Models;
using Microsoft.Extensions.Logging;

namespace api.Services.GameEngine;

/// <summary>
/// Yuva/kale seviye atlama mantığı ve doğrulaması (bkz. Bölüm 3.3).
/// Bu sınıftaki metotlar çağrılırken ilgili Match.Lock tutuluyor olmalıdır.
/// </summary>
public class UpgradeService
{
    private readonly ILogger<UpgradeService> _logger;

    public UpgradeService(ILogger<UpgradeService> logger)
    {
        _logger = logger;
    }

    public void Upgrade(Player player, Region region)
    {
        if (region.OwnerId != player.Id || region.Nest is null)
        {
            throw new InvalidOperationException("Bu bölgede size ait bir yuva yok.");
        }

        var nest = region.Nest;
        if (nest.Level >= GameConfig.MaxNestLevel)
        {
            throw new InvalidOperationException("Yuva zaten maksimum seviyede.");
        }

        var cost = nest.Level == 1 ? GameConfig.NestUpgradeToLevel2Cost : GameConfig.NestUpgradeToLevel3Cost;
        if (player.Gold < cost)
        {
            throw new InvalidOperationException("Yeterli altın yok.");
        }

        player.Gold -= cost;
        nest.Level += 1;
        nest.GarrisonArchers = nest.Level switch
        {
            2 => GameConfig.NestLevel2ArcherCount,
            3 => GameConfig.NestLevel3ArcherCount,
            _ => nest.GarrisonArchers
        };

        _logger.LogInformation("Yuva yükseltildi: {RegionId} -> seviye {Level}", region.Id, nest.Level);
    }
}
