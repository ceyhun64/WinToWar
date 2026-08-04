namespace api.Services.Payments;

public record ProviderInvoice(string BtcPayInvoiceId, string ReceivingAddress, string Bip21Uri);

public record ProviderTransferResult(string BtcPayTransactionId, decimal EstimatedFeeLtc);

/// <summary>
/// BTCPay Server Greenfield REST API v2'nin soyutlaması (Bölüm 1.3). Regtest/
/// testnet instance'ı bu görev sırasında erişilebilir olmadığından (Bölüm 0.3
/// ön koşulu), şu an yalnızca <see cref="FakePaymentProvider"/> implementasyonu
/// mevcuttur. Gerçek BTCPay entegrasyonu için bu interface'i implemente eden
/// yeni bir sınıf (ör. BtcPayGreenfieldProvider) eklenip Program.cs'te DI
/// kaydı değiştirilmesi yeterlidir — geri kalan tüm servisler (PaymentService,
/// PayoutService, RefundService, ReconciliationService) bu soyutlamaya bağımlı
/// olduğundan değişmeden çalışmaya devam eder.
/// </summary>
public interface IPaymentProvider
{
    Task<ProviderInvoice> CreateInvoiceAsync(
        string? matchId,
        string playerId,
        decimal amountLtc,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<ProviderTransferResult> SendPayoutAsync(
        string destinationAddress,
        decimal amountLtc,
        CancellationToken cancellationToken);

    Task<ProviderTransferResult> SendRefundAsync(
        string destinationAddress,
        decimal amountLtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reconciliation'ın kullandığı, bir transfer'in gerçekleşen (actual) on-chain
    /// fee'sini sorgulayan uç (Bölüm 2.6). Transfer henüz on-chain doğrulanmadıysa
    /// null döner.
    /// </summary>
    Task<decimal?> GetActualNetworkFeeAsync(string btcPayTransactionId, CancellationToken cancellationToken);
}
