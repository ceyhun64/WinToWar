namespace api.Models.Auth;

public enum AccountDeletionRequestStatus
{
    Pending,
    Completed,
    Rejected
}

/// <summary>docs/11-auth.md Bölüm 2.3.</summary>
public class AccountDeletionRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid PlayerId { get; init; }

    public DateTime RequestedAt { get; init; }

    public AccountDeletionRequestStatus Status { get; set; } = AccountDeletionRequestStatus.Pending;

    public string? RejectionReason { get; set; }
}
