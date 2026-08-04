using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers;

public record AdminMatchSummary(string MatchId, string Status, string RoomType, int PlayerCount, int MaxPlayers, string EntryFeeUsd);

public record AdminMatchEventDto(int SequenceNo, string EventType, string PayloadJson, DateTime OccurredAt);

/// <summary>
/// docs/07-pages.md `/admin/maclar`: aktif/geçmiş maç listesi. 🛠️ Oyun state'i
/// bellekte tutulduğundan (bkz. MatchManager) yalnızca süreç yeniden başlamadan
/// önceki maçlar listelenir — kalıcı bir geçmiş kaydı bu görevin kapsamı dışında
/// (bkz. `/gecmis`'in ödeme geçmişine dayandığı not). Denetim kaydı (MatchEventLog)
/// ise ayrıca kalıcıdır — bkz. GetEvents (docs/02-architecture.md "Maç Denetim Kaydı").
/// </summary>
[ApiController]
[AdminAuth]
[Route("api/admin/matches")]
public class AdminMatchesController : ControllerBase
{
    private readonly MatchManager _matchManager;
    private readonly GameEventDbContext _gameEventDb;

    public AdminMatchesController(MatchManager matchManager, GameEventDbContext gameEventDb)
    {
        _matchManager = matchManager;
        _gameEventDb = gameEventDb;
    }

    [HttpGet]
    public ActionResult<List<AdminMatchSummary>> GetAll()
    {
        var matches = _matchManager.ActiveMatches
            .Select(m => new AdminMatchSummary(
                m.Id,
                m.Status.ToString(),
                m.Room.Type.ToString(),
                m.Players.Count,
                m.Room.MaxPlayers,
                m.Room.EntryFeeUsd.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .OrderByDescending(m => m.Status)
            .ToList();

        return Ok(matches);
    }

    /// <summary>
    /// Ödeme itirazında ("maç gerçekten bu şekilde bitti mi") kanıt olarak kullanılacak
    /// ham denetim kaydı — genel kullanıcıya (`/gecmis`) asla gösterilmez, yalnızca admin.
    /// </summary>
    [HttpGet("{matchId}/events")]
    public async Task<ActionResult<List<AdminMatchEventDto>>> GetEvents(string matchId, CancellationToken cancellationToken)
    {
        var events = await _gameEventDb.MatchEventLogs
            .Where(e => e.MatchId == matchId)
            .OrderBy(e => e.SequenceNo)
            .Select(e => new AdminMatchEventDto(e.SequenceNo, e.EventType.ToString(), e.PayloadJson, e.OccurredAt))
            .ToListAsync(cancellationToken);

        return Ok(events);
    }
}
