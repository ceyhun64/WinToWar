using api.Models;

namespace api.Services.Payments;

public enum RoomEntryOutcome
{
    Joined,
    RoomFull,
    InsufficientBalance
}

public record RoomEntryResult(RoomEntryOutcome Outcome, decimal ShortfallUsd, string? PlayerId, int? Slot);

/// <summary>
/// docs/05-payment.md Bölüm 1.9: bir oyuncunun bir odaya (Room) katılım anındaki
/// Wallet ↔ MatchManager entegrasyon noktası — tek yerde toplanır ki hem senkron
/// "/api/rooms/*/join" akışı hem de webhook sonrası top-up-ve-katıl akışı
/// (PaymentService) aynı reservation+rollback mantığını kullansın (bkz.
/// `06-coding-standards.md` "Kod Tekrarını Önleme").
///
/// Wallet (SQL, PaymentDbContext) ve Match (bellek içi, MatchManager) iki ayrı
/// depodur — gerçek bir dağıtık transaction yoktur; bu yüzden sıra önemlidir:
/// önce Wallet'tan düş (kalıcı, geri alınabilir), sonra rezervasyonu dene; rezervasyon
/// başarısız olursa (oda dolmuş/başlamış) düşülen tutar hemen geri eklenir.
/// </summary>
public class RoomEntryService
{
    private readonly MatchManager _matchManager;
    private readonly WalletService _walletService;
    private readonly ILogger<RoomEntryService> _logger;

    public RoomEntryService(MatchManager matchManager, WalletService walletService, ILogger<RoomEntryService> logger)
    {
        _matchManager = matchManager;
        _walletService = walletService;
        _logger = logger;
    }

    public async Task<RoomEntryResult> TryJoinAsync(
        string matchId, string playerId, string playerName, DateTime now, CancellationToken cancellationToken, string? joinIpAddress = null)
    {
        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            return new RoomEntryResult(RoomEntryOutcome.RoomFull, 0m, null, null);
        }

        var entryFee = match.Room.EntryFeeUsd;
        if (entryFee <= 0)
        {
            var freePlayer = ReserveAndConfirmSafely(match, playerId, playerName, now, joinIpAddress);
            return freePlayer is null
                ? new RoomEntryResult(RoomEntryOutcome.RoomFull, 0m, null, null)
                : new RoomEntryResult(RoomEntryOutcome.Joined, 0m, freePlayer.Id, freePlayer.Slot);
        }

        var balance = await _walletService.GetBalanceAsync(playerId, cancellationToken);
        if (balance < entryFee)
        {
            return new RoomEntryResult(RoomEntryOutcome.InsufficientBalance, entryFee - balance, null, null);
        }

        var debited = await _walletService.TryDebitAsync(playerId, entryFee, cancellationToken);
        if (!debited)
        {
            // Yukarıdaki GetBalanceAsync yalnızca iyimser bir ön kontroldür — asıl
            // garanti TryDebitAsync'in kendi atomik UPDATE'idir (bkz. WalletService).
            // Eşzamanlı bir başka işlem bakiyeyi bu ikisi arasında tüketmiş olabilir;
            // bu durumda TryDebitAsync güvenle false döner, hiçbir yarış/kayıp
            // güncelleme oluşmaz.
            return new RoomEntryResult(RoomEntryOutcome.InsufficientBalance, entryFee, null, null);
        }

        var player = ReserveAndConfirmSafely(match, playerId, playerName, now, joinIpAddress);
        if (player is null)
        {
            await _walletService.CreditAsync(playerId, entryFee, cancellationToken);
            _logger.LogInformation("Oda dolu/başlamış, düşülen giriş ücreti bakiyeye geri eklendi: {PlayerId}, {MatchId}", playerId, matchId);
            return new RoomEntryResult(RoomEntryOutcome.RoomFull, 0m, null, null);
        }

        return new RoomEntryResult(RoomEntryOutcome.Joined, 0m, player.Id, player.Slot);
    }

    /// <summary>
    /// VIP oda kurucusu, oda oluşturulurken zaten 1. slota rezerve edilmiş olabilir
    /// (yalnızca ödemesi onaylanmamış) — bu durumda tekrar ReservePlayer çağrılmaz,
    /// doğrudan onaylanır. Diğer tüm katılımlarda (Standart/VIP/davet linki) oyuncu
    /// henüz hiç rezerve edilmemiştir, burada ilk kez rezerve edilir.
    /// </summary>
    private Player? ReserveAndConfirmSafely(Match match, string playerId, string playerName, DateTime now, string? joinIpAddress)
    {
        var alreadyReserved = match.Players.FirstOrDefault(p => p.Id == playerId);
        if (alreadyReserved is not null)
        {
            if (match.Status != MatchStatus.Lobby)
            {
                return null;
            }

            _matchManager.ConfirmPlayerPayment(match.Id, alreadyReserved.Id, now);
            return alreadyReserved;
        }

        try
        {
            var player = _matchManager.ReservePlayer(match, playerName, now, forcedPlayerId: playerId, joinIpAddress: joinIpAddress);
            _matchManager.ConfirmPlayerPayment(match.Id, player.Id, now);
            return player;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
