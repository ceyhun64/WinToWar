using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Services;

/// <summary>
/// docs/02-architecture.md "Maç Denetim Kaydı": <see cref="MatchEventLogWriter"/>'ın
/// salt-okunur karşılığı — o sınıf bilerek DbContext'e dokunmaz (sıcak yolda senkron
/// DB yazımı olmasın diye, bkz. kendi yorumu), bu yüzden admin'in denetim kaydını
/// okuması ayrı, dar kapsamlı bir sınıfa aittir (docs/09-eksik-tarama-promptu.md
/// denetimi, Faz 8 — Controller'ın doğrudan GameEventDbContext sorgulaması
/// Controller→Service→Model kuralını ihlal ediyordu).
/// </summary>
public class MatchEventLogReader
{
    private readonly GameEventDbContext _db;

    public MatchEventLogReader(GameEventDbContext db)
    {
        _db = db;
    }

    public async Task<List<MatchEventLog>> GetEventsAsync(string matchId, CancellationToken cancellationToken)
    {
        return await _db.MatchEventLogs
            .Where(e => e.MatchId == matchId)
            .OrderBy(e => e.SequenceNo)
            .ToListAsync(cancellationToken);
    }
}
