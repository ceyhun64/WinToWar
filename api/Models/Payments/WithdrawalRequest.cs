namespace api.Models.Payments;

/// <summary>
/// Bölüm 1.9: oyuncu-başlatımlı, kazanç/Payout akışından bağımsız bir para çekme
/// talebi (Refund'dan farklıdır — Refund sistem tetiklemeli bir iadedir). Talep
/// oluşturulduğu an Wallet.BalanceUsd'den düşülür; Failed/Rejected durumunda geri
/// eklenir (bkz. WalletService.RequestWithdrawalAsync).
/// </summary>
public class WithdrawalRequest
{
    public Guid Id { get; set; }
    public required string PlayerId { get; set; }
    public decimal AmountUsd { get; set; }

    /// <summary>Talep anında kilitlenen kur ile hesaplanmış, yalnızca gösterim/işlem amaçlı.</summary>
    public decimal AmountLtc { get; set; }

    public required string DestinationLtcAddress { get; set; }
    public WithdrawalRequestStatus Status { get; set; } = WithdrawalRequestStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
