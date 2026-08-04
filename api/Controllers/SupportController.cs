using api.Services;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public record CreateSupportTicketRequest(string Subject, string Description, string ContactEmail, string? MatchId);
public record SupportTicketResponse(string Id);

/// <summary>docs/07-pages.md `/destek`: basit iletişim formu.</summary>
[ApiController]
[Route("api/support-tickets")]
public class SupportController : ControllerBase
{
    private readonly SupportTicketStore _store;
    private readonly ILogger<SupportController> _logger;

    public SupportController(SupportTicketStore store, ILogger<SupportController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpPost]
    public ActionResult<SupportTicketResponse> Create([FromBody] CreateSupportTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            return BadRequest("Konu, açıklama ve e-posta gereklidir.");
        }

        var ticket = _store.Add(request.Subject.Trim(), request.Description.Trim(), request.ContactEmail.Trim(), request.MatchId);
        _logger.LogInformation("Destek talebi oluşturuldu: {TicketId}", ticket.Id);
        return Ok(new SupportTicketResponse(ticket.Id));
    }
}
