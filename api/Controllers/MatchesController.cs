using api.Models.Dtos;
using api.Services;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace api.Controllers;

/// <summary>
/// Statik harita verisi ve GameConfig'in client'a yansıtılan alt kümesi için REST
/// uçları. Oda/lobi oluşturma-katılma RoomsController üzerinden yürütülür; gerçek
/// zamanlı oyun aksiyonları ve state broadcast'i GameHub üzerinden (SignalR) yürütülür.
/// `{matchId}` uçları yalnızca bir anlık görüntü (snapshot) döner — docs/07-pages.md
/// `/mac/[matchId]` gibi SignalR bağlantısı gerektirmeyen salt-okunur sayfalar için.
/// </summary>
[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    private readonly MapProvider _mapProvider;
    private readonly MatchManager _matchManager;
    private readonly PayoutService _payoutService;
    private readonly PaymentConfig _paymentConfig;

    public MatchesController(MapProvider mapProvider, MatchManager matchManager, PayoutService payoutService, IOptions<PaymentConfig> paymentConfig)
    {
        _mapProvider = mapProvider;
        _matchManager = matchManager;
        _payoutService = payoutService;
        _paymentConfig = paymentConfig.Value;
    }

    [HttpGet("map")]
    public ActionResult<MapDto> GetMap()
    {
        return Ok(MatchStateMapper.ToMapDto(_mapProvider.Map));
    }

    [HttpGet("config")]
    public ActionResult<GameConfigDto> GetConfig()
    {
        return Ok(GameConfigDto.FromGameConfig(_paymentConfig.CommissionRate));
    }

    [HttpGet("{matchId}")]
    public ActionResult<MatchStateDto> GetMatch(string matchId)
    {
        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            return NotFound();
        }

        MatchStateDto dto;
        lock (match.Lock)
        {
            dto = MatchStateMapper.ToDto(match, DateTime.UtcNow);
        }

        return Ok(dto);
    }

    [HttpGet("{matchId}/payout")]
    public async Task<ActionResult<Models.Payments.Dtos.PayoutSummaryDto>> GetPayout(string matchId, CancellationToken cancellationToken)
    {
        var summary = await _payoutService.GetPayoutSummaryAsync(matchId, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }
}
