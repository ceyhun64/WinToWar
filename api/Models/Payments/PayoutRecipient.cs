namespace api.Models.Payments;

/// <summary>
/// Kazanan başına bir satır (Payout ile 1-N, N=1 normal senaryoda). Kazanç
/// doğrudan bu oyuncunun Wallet.BalanceUsd'sine kredi olarak işlenir (bkz.
/// PayoutService.ProcessPayoutAsync) — LTC adresi/on-chain gönderim bu akışın
/// bir parçası değildir, oyuncu isterse bakiyesini ayrıca /cuzdan üzerinden
/// WithdrawalRequest ile çeker. Unique constraint: (PayoutId, WinnerPlayerId) —
/// aynı kazanan için aynı Payout'a iki kez satır yazılamaz (idempotency, ikinci katman).
/// </summary>
public class PayoutRecipient
{
    public Guid Id { get; set; }
    public Guid PayoutId { get; set; }
    public required string WinnerPlayerId { get; set; }

    /// <summary>Bu kazanana kredi olarak eklenen tutar — Wallet.BalanceUsd'ye aynen yansır.</summary>
    public decimal AmountUsd { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
