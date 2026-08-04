namespace api.Models;

public enum SupportTicketStatus
{
    Open,
    Answered,
    Closed
}

/// <summary>
/// docs/07-pages.md `/destek` + `/admin/destek`: basit bir iletişim formu kaydı.
/// YAGNI gereği ayrı bir SQL tablosu/migration yerine bellek içi bir depo
/// yeterlidir (bkz. SupportTicketStore) — oyun state'i de aynı şekilde bellekte
/// tutuluyor (bkz. MatchManager).
/// </summary>
public class SupportTicket
{
    public required string Id { get; init; }
    public required string Subject { get; init; }
    public required string Description { get; init; }
    public required string ContactEmail { get; init; }
    public string? MatchId { get; init; }
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    public DateTime CreatedAtUtc { get; init; }
}
