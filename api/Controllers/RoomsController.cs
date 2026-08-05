using System.Globalization;
using api.Models.Rooms;
using api.Services;
using api.Services.Payments;
using api.Services.Rooms;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// 🛠️ PlayerId, client tarafından üretilip localStorage'da kalıcı olarak saklanan
/// bir kimliktir (auth modülü henüz netleşmedi — bkz. Wallet.cs) — Wallet/oda
/// rezervasyonu bu id üzerinden eşleşir. PayoutAddress, ücretsiz (Practice) olmayan
/// her katılımda gereklidir — bakiye yeterliyse kazanılırsa ödülün gönderileceği
/// adres olarak Wallet'a kaydedilir, yetersizse ayrıca eksik tutar invoice'ının
/// PayoutAddress'i olarak kullanılır. Daha önce sağlanmış bir adres varsa (Wallet'ta
/// kayıtlı) tekrar gönderilmesi gerekmez — null bırakılabilir.
/// </summary>
public record JoinRoomRequest(string PlayerId, string PlayerName, string? PayoutAddress);

public record CreateVipRoomRequest(
    string PlayerId,
    string PlayerName,
    int MaxPlayers,
    int GreyRegionDefenseCount,
    bool FogOfWar,
    decimal EntryFeeUsd,
    string? Password,
    string? PayoutAddress);

public record JoinRoomResult(
    string Outcome,
    string? MatchId,
    string? PlayerId,
    int? Slot,
    string? ShortfallUsd,
    api.Models.Payments.Dtos.PaymentInvoiceDto? Invoice);

public record RoomSummaryResponse(string MatchId, string RoomName, int PlayerCount, int MaxPlayers, string EntryFeeUsd, bool FogOfWar, int GreyRegionDefenseCount, bool IsPasswordProtected);

public record VerifyRoomPasswordRequest(string Password);

/// <summary>
/// Oda oluşturma/listeleme/katılma REST uçları (docs/03-game-rules.md Bölüm 2,
/// docs/07-pages.md `/lobi`, `/lobi/vip-olustur`, `/lobi/[inviteToken]`). Giriş
/// ücretinin tahsilatı (Wallet düşümü / top-up-ve-katıl invoice akışı,
/// docs/05-payment.md Bölüm 1.9) burada RoomEntryService üzerinden aynı istekte
/// yürütülür — bakiye yeterliyse doğrudan katılım, yetmiyorsa eksik tutar (ve
/// PayoutAddress verildiyse hazır bir invoice) döner.
/// </summary>
[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly RoomService _roomService;
    private readonly MatchManager _matchManager;
    private readonly RoomEntryService _roomEntryService;
    private readonly PaymentService _paymentService;

    public RoomsController(RoomService roomService, MatchManager matchManager, RoomEntryService roomEntryService, PaymentService paymentService)
    {
        _roomService = roomService;
        _matchManager = matchManager;
        _roomEntryService = roomEntryService;
        _paymentService = paymentService;
    }

    [HttpGet]
    public ActionResult<List<RoomSummaryResponse>> ListOpenRooms([FromQuery] RoomType type)
    {
        var rooms = _roomService.ListOpenRooms(type).Select(ToRoomSummaryResponse).ToList();
        return Ok(rooms);
    }

    [HttpPost("standard/join")]
    public async Task<ActionResult<JoinRoomResult>> JoinStandard([FromBody] JoinRoomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName) || string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest("Oyuncu adı ve kimliği gereklidir.");
        }

        var match = _roomService.FindOrCreateStandardMatch(DateTime.UtcNow);
        return Ok(await TryJoinAndRespondAsync(match.Id, request.PlayerId, request.PlayerName.Trim(), request.PayoutAddress, cancellationToken));
    }

    [HttpPost("practice/join")]
    public ActionResult<JoinRoomResult> JoinPractice([FromBody] JoinRoomRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName) || string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest("Oyuncu adı ve kimliği gereklidir.");
        }

        var (match, player) = _roomService.JoinPracticeQueue(request.PlayerName.Trim(), DateTime.UtcNow);
        return Ok(new JoinRoomResult("Joined", match.Id, player.Id, player.Slot, null, null));
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 2.2: kurucu, formu gönderdiği anda odanın 1.
    /// slotuna rezerve edilir ve giriş ücretini kendisi de öder — bakiye yetmezse
    /// oda yine de kurulmuş olarak kalır (kurucu ödemesi onaylanmamış), döndürülen
    /// matchId/playerId ile `/api/matches/{matchId}/payments` üzerinden ödenebilir.
    /// </summary>
    [HttpPost("vip")]
    public async Task<ActionResult<JoinRoomResult>> CreateVipRoom([FromBody] CreateVipRoomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName) || string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest("Oyuncu adı ve kimliği gereklidir.");
        }

        Models.Match match;
        try
        {
            (match, _) = _roomService.CreateVipRoom(
                request.PlayerId,
                request.PlayerName.Trim(),
                request.MaxPlayers,
                request.GreyRegionDefenseCount,
                request.FogOfWar,
                request.EntryFeeUsd,
                request.Password,
                DateTime.UtcNow,
                HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok(await TryJoinAndRespondAsync(match.Id, request.PlayerId, request.PlayerName.Trim(), request.PayoutAddress, cancellationToken));
    }

    [HttpGet("invite/{inviteToken}")]
    public ActionResult<RoomSummaryResponse> GetByInviteToken(string inviteToken)
    {
        var match = _roomService.FindByInviteToken(inviteToken);
        if (match is null)
        {
            return NotFound();
        }

        return Ok(ToRoomSummaryResponse(match));
    }

    /// <summary>
    /// docs/08-page-content.md Bölüm 3.4: liste ve davet-token uçlarının ikisi de
    /// aynı oda kimliği türetme mantığını (RoomDisplayNameFormatter) kullanır —
    /// bkz. `06-coding-standards.md` "Kod Tekrarını Önleme".
    /// </summary>
    private static RoomSummaryResponse ToRoomSummaryResponse(Models.Match match)
    {
        var creatorName = match.Players.FirstOrDefault(p => p.Id == match.Room.CreatorPlayerId)?.Name;
        return new RoomSummaryResponse(
            match.Id,
            RoomDisplayNameFormatter.Format(match.Room.Type, creatorName),
            match.Players.Count,
            match.Room.MaxPlayers,
            match.Room.EntryFeeUsd.ToString(CultureInfo.InvariantCulture),
            match.Room.FogOfWar,
            match.Room.GreyRegionDefenseCount,
            match.Room.IsPasswordProtected);
    }

    [HttpPost("{matchId}/verify-password")]
    public ActionResult<bool> VerifyPassword(string matchId, [FromBody] VerifyRoomPasswordRequest request)
    {
        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            return NotFound();
        }

        return Ok(_roomService.VerifyPassword(match.Room, request.Password));
    }

    /// <summary>Şifreli/davet linkli VIP odaya veya belirli bir Standart odaya doğrudan katılım.</summary>
    [HttpPost("{matchId}/join")]
    public async Task<ActionResult<JoinRoomResult>> JoinSpecificRoom(string matchId, [FromBody] JoinRoomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName) || string.IsNullOrWhiteSpace(request.PlayerId))
        {
            return BadRequest("Oyuncu adı ve kimliği gereklidir.");
        }

        if (!_matchManager.TryGetMatch(matchId, out _))
        {
            return NotFound("Oda bulunamadı.");
        }

        return Ok(await TryJoinAndRespondAsync(matchId, request.PlayerId, request.PlayerName.Trim(), request.PayoutAddress, cancellationToken));
    }

    private async Task<JoinRoomResult> TryJoinAndRespondAsync(
        string matchId, string playerId, string playerName, string? payoutAddress, CancellationToken cancellationToken)
    {
        var joinIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _roomEntryService.TryJoinAsync(matchId, playerId, playerName, payoutAddress, DateTime.UtcNow, cancellationToken, joinIpAddress);

        switch (result.Outcome)
        {
            case RoomEntryOutcome.Joined:
                return new JoinRoomResult("Joined", matchId, result.PlayerId, result.Slot, null, null);

            case RoomEntryOutcome.RoomFull:
                return new JoinRoomResult("RoomFull", matchId, null, null, null, null);

            case RoomEntryOutcome.PayoutAddressRequired:
                return new JoinRoomResult("PayoutAddressRequired", matchId, null, null, null, null);

            case RoomEntryOutcome.InvalidPayoutAddress:
                return new JoinRoomResult("InvalidPayoutAddress", matchId, null, null, null, null);

            default: // InsufficientBalance
                var shortfallText = result.ShortfallUsd.ToString(CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(payoutAddress))
                {
                    return new JoinRoomResult("InsufficientBalance", matchId, null, null, shortfallText, null);
                }

                try
                {
                    var invoice = await _paymentService.CreateMatchEntryInvoiceAsync(matchId, playerId, playerName, payoutAddress, cancellationToken);
                    return new JoinRoomResult("InsufficientBalance", matchId, null, null, shortfallText, invoice);
                }
                catch (PaymentValidationException)
                {
                    return new JoinRoomResult("InsufficientBalance", matchId, null, null, shortfallText, null);
                }
        }
    }
}
