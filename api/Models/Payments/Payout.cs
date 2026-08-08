namespace api.Models.Payments;

/// <summary>
/// Maç başına tam olarak bir satır — agregatör. Kazanç artık on-chain LTC olarak
/// değil, doğrudan kazananların Wallet.BalanceUsd'sine kredi olarak işlendiğinden
/// (bkz. PayoutService.ProcessPayoutAsync), bu satır tek bir atomik transaction
/// içinde kalıcılaşır — ayrı bir "gönderim beklemede/gönderildi" ara durumu yoktur.
/// </summary>
public class Payout
{
    public Guid Id { get; set; }

    /// <summary>İdempotency anahtarı — maça yalnızca bir Payout (agregatör) satırı yazılır.</summary>
    public required string MatchId { get; set; }

    /// <summary>Odaya katılan onaylı oyuncu sayısının toplam girişi (Room.EntryFeeUsd × fiilen ödemesi onaylanmış oyuncu sayısı).</summary>
    public decimal TotalPoolUsd { get; set; }
    public decimal CommissionUsd { get; set; }

    /// <summary>Match.Winners.Count — normal senaryoda 1, beraberlikte N.</summary>
    public int WinnerCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
