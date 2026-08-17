namespace api.Services.Payments;

public record ProviderInvoice(string BtcPayInvoiceId, string ReceivingAddress, string Bip21Uri);

public record ProviderTransferResult(string BtcPayTransactionId, decimal EstimatedFeeLtc);

/// <summary>
/// BTCPay Server Greenfield REST API v2'nin soyutlaması (Bölüm 1.3). İki
/// implementasyonu vardır ve seçim <c>Payment:Mode</c> ile yapılır (bkz. Program.cs):
/// ağa hiç çıkmayan <see cref="FakePaymentProvider"/> (`Fake`) ve gerçek
/// <see cref="BtcPayGreenfieldProvider"/> (`Sandbox`/`Live`). Üstteki servisler
/// (PaymentService, PayoutService, RefundService, WalletService) yalnızca bu
/// soyutlamaya bağımlıdır, mod değişince değişmezler.
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

    /// <summary>
    /// docs/15-payment-flow-verification.md gerçek regtest E2E bulgusu: bir
    /// invoice için fiilen ödenen toplam tutar webhook payload'ında hiç
    /// bulunmuyor (yalnızca event tipi + invoice id taşır) — bu yüzden Bölüm 1.2/1.7'deki
    /// overpayment/tolerans kontrolü webhook body'sinden değil, doğrudan bu uçtan
    /// okunur. Henüz hiçbir ödeme algılanmadıysa null döner.
    /// </summary>
    Task<decimal?> GetTotalPaidLtcAsync(string btcPayInvoiceId, CancellationToken cancellationToken);
}
