using System.Collections.Concurrent;
using api.Models;

namespace api.Services;

/// <summary>Bellek içi destek talebi deposu (bkz. SupportTicket.cs gerekçesi).</summary>
public class SupportTicketStore
{
    private readonly ConcurrentDictionary<string, SupportTicket> _tickets = new();

    public SupportTicket Add(string subject, string description, string contactEmail, string? matchId)
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid().ToString("N"),
            Subject = subject,
            Description = description,
            ContactEmail = contactEmail,
            MatchId = matchId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _tickets[ticket.Id] = ticket;
        return ticket;
    }

    public List<SupportTicket> GetAll() => _tickets.Values.OrderByDescending(t => t.CreatedAtUtc).ToList();

    public bool TryUpdateStatus(string id, SupportTicketStatus status)
    {
        if (!_tickets.TryGetValue(id, out var ticket))
        {
            return false;
        }

        ticket.Status = status;
        return true;
    }
}
