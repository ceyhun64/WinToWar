using api.Models;
using api.Models.Rooms;
using api.Services;

namespace api.Tests.TestSupport;

/// <summary>
/// v9: PayoutService artık havuzu Room.EntryFeeUsd × onaylı oyuncu sayısından
/// hesaplıyor (bkz. PayoutService.ProcessPayoutAsync gerekçesi) — bu yüzden
/// payout testleri gerçek bir Match'e (MatchManager üzerinden, onaylı oyuncularla)
/// ihtiyaç duyar, yalnızca PaymentInvoice kaydı yeterli değildir. MatchManager
/// kendi Id'sini ürettiğinden (bkz. Match.Id init-only), çağıran taraf üretilen
/// gerçek Id'yi (dönüş değerindeki Match.Id) kullanmalıdır.
/// </summary>
public static class MatchFactory
{
    public static (Match Match, List<Player> Players) CreateConfirmedVipMatch(
        MatchManager matchManager, decimal entryFeeUsd, IReadOnlyList<string> playerIds, DateTime now)
    {
        var room = new Room
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = RoomType.Vip,
            MaxPlayers = playerIds.Count,
            GreyRegionDefenseCount = 1,
            FogOfWar = false,
            EntryFeeUsd = entryFeeUsd,
            CreatorPlayerId = playerIds[0]
        };

        var match = matchManager.CreateMatch(room, now);

        var players = new List<Player>();
        foreach (var playerId in playerIds)
        {
            var player = matchManager.ReservePlayer(match, playerId, now, forcedPlayerId: playerId);
            matchManager.ConfirmPlayerPayment(match.Id, player.Id, now);
            players.Add(player);
        }

        return (match, players);
    }
}
