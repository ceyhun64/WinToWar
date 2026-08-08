using System.Globalization;
using System.Security.Claims;
using api.Models.Rooms;
using api.Models.Rooms.Dtos;
using api.Services;
using api.Services.Payments;
using api.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// docs/11-auth.md Bölüm 0.4: PlayerId artık istemciden alınmaz, JWT'nin sub
/// claim'inden okunur (bkz. RoomsController.CurrentPlayerId) — Wallet/oda
/// rezervasyonu bu id üzerinden eşleşir. Katılım hiçbir LTC adresi istemez —
/// giriş ücreti doğrudan Wallet.BalanceUsd'den düşülür, yetmezse bir top-up
/// invoice'ı açılır (bkz. RoomEntryService, PaymentService).
/// </summary>
public record JoinRoomRequest(string PlayerName);

public record CreateVipRoomRequest(
    string PlayerName,
    int MaxPlayers,
    int GreyRegionDefenseCount,
    bool FogOfWar,
    decimal EntryFeeUsd,
    string? Password);

public record JoinRoomResult(
    string Outcome,
    string? MatchId,
    string? PlayerId,
    int? Slot,
    string? ShortfallUsd,
    api.Models.Payments.Dtos.PaymentInvoiceDto? Invoice);

public record VerifyRoomPasswordRequest(string Password);

/// <summary>
/// Oda oluşturma/listeleme/katılma REST uçları (docs/03-game-rules.md Bölüm 2,
/// docs/07-pages.md `/lobi`, `/lobi/vip-olustur`, `/lobi/[inviteToken]`). Giriş
/// ücretinin tahsilatı (Wallet düşümü / top-up-ve-katıl invoice akışı,
/// docs/05-payment.md Bölüm 1.9) burada RoomEntryService üzerinden aynı istekte
/// yürütülür — bakiye yeterliyse doğrudan katılım, yetmiyorsa eksik tutar için
/// sunucu doğrudan bir invoice döner (hiçbir adım LTC adresi istemez).
/// </summary>
[Authorize]
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

    private string CurrentPlayerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public ActionResult<List<RoomSummaryResponse>> ListOpenRooms([FromQuery] RoomType type)
    {
        var rooms = _roomService.ListOpenRooms(type).Select(_roomService.ToRoomSummaryResponse).ToList();
        return Ok(rooms);
    }

    [HttpPost("standard/join")]
    public async Task<ActionResult<JoinRoomResult>> JoinStandard([FromBody] JoinRoomRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("Oyuncu adı gereklidir.");
        }

        var match = _roomService.FindOrCreateStandardMatch(DateTime.UtcNow);
        return Ok(await TryJoinAndRespondAsync(match.Id, CurrentPlayerId, request.PlayerName.Trim(), cancellationToken));
    }

    [HttpPost("practice/join")]
    public ActionResult<JoinRoomResult> JoinPractice([FromBody] JoinRoomRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("Oyuncu adı gereklidir.");
        }

        var (match, player) = _roomService.JoinPracticeQueue(CurrentPlayerId, request.PlayerName.Trim(), DateTime.UtcNow);
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
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("Oyuncu adı gereklidir.");
        }

        Models.Match match;
        try
        {
            (match, _) = _roomService.CreateVipRoom(
                CurrentPlayerId,
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

        return Ok(await TryJoinAndRespondAsync(match.Id, CurrentPlayerId, request.PlayerName.Trim(), cancellationToken));
    }

    [HttpGet("invite/{inviteToken}")]
    public ActionResult<RoomSummaryResponse> GetByInviteToken(string inviteToken)
    {
        var match = _roomService.FindByInviteToken(inviteToken);
        if (match is null)
        {
            return NotFound();
        }

        return Ok(_roomService.ToRoomSummaryResponse(match));
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
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("Oyuncu adı gereklidir.");
        }

        if (!_matchManager.TryGetMatch(matchId, out _))
        {
            return NotFound("Oda bulunamadı.");
        }

        return Ok(await TryJoinAndRespondAsync(matchId, CurrentPlayerId, request.PlayerName.Trim(), cancellationToken));
    }

    /// <summary>
    /// Katılım hiçbir LTC adresi istemez. Bakiye yetersizse (Practice hariç her
    /// oda için) sunucu her zaman doğrudan bir top-up invoice'ı açar — kullanıcı
    /// BTCPay'in kendi ürettiği ödeme adresi/QR'ı ile öder (bkz. docs/05-payment.md
    /// Bölüm 1.9), ayrıca bir adres girmesi istenmez.
    /// </summary>
    private async Task<JoinRoomResult> TryJoinAndRespondAsync(
        string matchId, string playerId, string playerName, CancellationToken cancellationToken)
    {
        var joinIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _roomEntryService.TryJoinAsync(matchId, playerId, playerName, DateTime.UtcNow, cancellationToken, joinIpAddress);

        switch (result.Outcome)
        {
            case RoomEntryOutcome.Joined:
                return new JoinRoomResult("Joined", matchId, result.PlayerId, result.Slot, null, null);

            case RoomEntryOutcome.RoomFull:
                return new JoinRoomResult("RoomFull", matchId, null, null, null, null);

            default: // InsufficientBalance
                var shortfallText = result.ShortfallUsd.ToString(CultureInfo.InvariantCulture);
                try
                {
                    var invoice = await _paymentService.CreateMatchEntryInvoiceAsync(matchId, playerId, playerName, cancellationToken);
                    return new JoinRoomResult("InsufficientBalance", matchId, null, null, shortfallText, invoice);
                }
                catch (PaymentValidationException)
                {
                    return new JoinRoomResult("InsufficientBalance", matchId, null, null, shortfallText, null);
                }
        }
    }
}
