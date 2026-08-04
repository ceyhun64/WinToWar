namespace api.Models.Payments;

/// <summary>
/// Bölüm 2.2 (v8): maç başına tam olarak bir satır — agregatör. Kendi rank'i
/// yoktur, doğrudan webhook almaz; Status yalnızca kendi <see cref="PayoutRecipient"/>
/// çocuklarının durumundan türetilir (bkz. PayoutService.RecalculateAggregateStatusAsync).
/// </summary>
public class Payout
{
    public Guid Id { get; set; }

    /// <summary>İdempotency anahtarı — maça yalnızca bir Payout (agregatör) satırı yazılır.</summary>
    public required string MatchId { get; set; }

    /// <summary>12 oyuncunun toplam girişi (yuvarlanmamış ara değerden tek seferde yuvarlanmış).</summary>
    public decimal TotalPoolLtc { get; set; }
    public decimal CommissionLtc { get; set; }

    /// <summary>Match.Winners.Count — normal senaryoda 1, beraberlikte N.</summary>
    public int WinnerCount { get; set; }

    public PayoutStatus Status { get; set; } = PayoutStatus.PayoutPending;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
