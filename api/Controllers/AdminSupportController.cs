using api.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public record AdminSupportTicketResponse(string Id, string Subject, string Description, string ContactEmail, string? MatchId, string Status, DateTime CreatedAtUtc);
public record UpdateSupportTicketStatusRequest(SupportTicketStatus Status);

/// <summary>docs/07-pages.md `/admin/destek`: bekleyen destek talepleri, durum güncelleme.</summary>
[ApiController]
[AdminAuth]
[Route("api/admin/support-tickets")]
public class AdminSupportController : ControllerBase
{
    private readonly SupportTicketStore _store;

    public AdminSupportController(SupportTicketStore store)
    {
        _store = store;
    }

    [HttpGet]
    public ActionResult<List<AdminSupportTicketResponse>> GetAll()
    {
        var tickets = _store.GetAll()
            .Select(t => new AdminSupportTicketResponse(t.Id, t.Subject, t.Description, t.ContactEmail, t.MatchId, t.Status.ToString(), t.CreatedAtUtc))
            .ToList();
        return Ok(tickets);
    }

    [HttpPost("{id}/status")]
    public IActionResult UpdateStatus(string id, [FromBody] UpdateSupportTicketStatusRequest request)
    {
        return _store.TryUpdateStatus(id, request.Status) ? Ok() : NotFound();
    }
}
