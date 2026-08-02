using api.Models.Dtos;
using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public record CreateMatchRequest(string PlayerName);

public record JoinMatchRequest(string PlayerName);

public record JoinMatchResponse(string MatchId, string PlayerId, int Slot);

/// <summary>
/// Maç oluşturma/katılma ve statik harita verisi için REST uçları. Gerçek zamanlı
/// oyun aksiyonları ve state broadcast'i GameHub üzerinden (SignalR) yürütülür.
/// </summary>
[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    private readonly MatchManager _matchManager;
    private readonly MapProvider _mapProvider;

    public MatchesController(MatchManager matchManager, MapProvider mapProvider)
    {
        _matchManager = matchManager;
        _mapProvider = mapProvider;
    }

    [HttpGet("map")]
    public ActionResult<MapDto> GetMap()
    {
        return Ok(MatchStateMapper.ToMapDto(_mapProvider.Map));
    }

    [HttpGet("config")]
    public ActionResult<GameConfigDto> GetConfig()
    {
        return Ok(new GameConfigDto
        {
            SoldierCost = GameConfig.SoldierCost,
            GeneralCost = GameConfig.GeneralCost,
            NestUpgradeToLevel2Cost = GameConfig.NestUpgradeToLevel2Cost,
            NestUpgradeToLevel3Cost = GameConfig.NestUpgradeToLevel3Cost,
            MaxNestLevel = GameConfig.MaxNestLevel,
            MaxGeneralsPerPlayer = GameConfig.MaxGeneralsPerPlayer,
            MatchDurationSeconds = GameConfig.MatchDurationSeconds
        });
    }

    [HttpPost]
    public ActionResult<JoinMatchResponse> CreateMatch([FromBody] CreateMatchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("Oyuncu adı gereklidir.");
        }

        var match = _matchManager.CreateMatch();
        var player = _matchManager.AddPlayer(match, request.PlayerName.Trim());
        return Ok(new JoinMatchResponse(match.Id, player.Id, player.Slot));
    }

    [HttpPost("{matchId}/join")]
    public ActionResult<JoinMatchResponse> JoinMatch(string matchId, [FromBody] JoinMatchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("Oyuncu adı gereklidir.");
        }

        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            return NotFound("Maç bulunamadı.");
        }

        try
        {
            var player = _matchManager.AddPlayer(match, request.PlayerName.Trim());
            return Ok(new JoinMatchResponse(match.Id, player.Id, player.Slot));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
