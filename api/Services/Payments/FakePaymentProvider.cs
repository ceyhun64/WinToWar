using System.Globalization;
using System.Security.Cryptography;

namespace api.Services.Payments;

/// <summary>
/// BTCPay regtest/testnet instance'ı bu görev sırasında erişilemediği için
/// (docs/05-payment.md Bölüm 0.3 ön koşulu) kullanılan sahte implementasyon.
/// Gerçek para hareketi YAPMAZ; invoice/payout/refund yaşam döngüsünü simüle
/// eder. Üretilen adresler ve transaction id'leri gerçek zincirde doğrulanabilir
/// değildir — yalnızca geliştirme/demo amaçlıdır (bkz. PaymentsDevController).
///
/// Gerçek entegrasyon: bu sınıfın yerine IPaymentProvider'ı implemente eden bir
/// BtcPayGreenfieldProvider yazılıp Program.cs'teki DI kaydı değiştirilecek;
/// PaymentService/PayoutService/RefundService/ReconciliationService değişmeden çalışır.
/// </summary>
public class FakePaymentProvider : IPaymentProvider
{
    // 🛠️ Varsayım: gerçek bir cüzdan/on-chain fee tahmini olmadığından sabit ama
    // gerçekçi bir tahmini fee kullanılır. Reconciliation'ın "actual" fee'yi
    // tahminden AYRI ve FARKLI bir değerle doldurduğunu gözlemlenebilir kılmak
    // için actual fee, tahminden kasıtlı olarak farklı simüle edilir.
    private const decimal SimulatedEstimatedFeeLtc = 0.00050000m;
    private const decimal SimulatedActualFeeLtc = 0.00041230m;

    private readonly ILogger<FakePaymentProvider> _logger;

    public FakePaymentProvider(ILogger<FakePaymentProvider> logger)
    {
        _logger = logger;
    }

    public Task<ProviderInvoice> CreateInvoiceAsync(
        string matchId, string playerId, decimal amountLtc, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var invoiceId = $"fake-inv-{Guid.NewGuid():N}";
        var address = GenerateFakeAddress();
        var amountText = amountLtc.ToString("0.00000000", CultureInfo.InvariantCulture);
        var uri = $"litecoin:{address}?amount={amountText}";

        _logger.LogInformation(
            "[FakePaymentProvider] Invoice oluşturuldu: {InvoiceId}, match={MatchId}, oyuncu={PlayerId}, tutar={AmountLtc} LTC",
            invoiceId, matchId, playerId, amountLtc);

        return Task.FromResult(new ProviderInvoice(invoiceId, address, uri));
    }

    public Task<ProviderTransferResult> SendPayoutAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken)
    {
        var txId = $"fake-payout-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "[FakePaymentProvider] Payout gönderildi (simüle): {TxId}, hedef={Address}, tutar={AmountLtc} LTC",
            txId, destinationAddress, amountLtc);
        return Task.FromResult(new ProviderTransferResult(txId, SimulatedEstimatedFeeLtc));
    }

    public Task<ProviderTransferResult> SendRefundAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken)
    {
        var txId = $"fake-refund-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "[FakePaymentProvider] Refund gönderildi (simüle): {TxId}, hedef={Address}, tutar={AmountLtc} LTC",
            txId, destinationAddress, amountLtc);
        return Task.FromResult(new ProviderTransferResult(txId, SimulatedEstimatedFeeLtc));
    }

    public Task<decimal?> GetActualNetworkFeeAsync(string btcPayTransactionId, CancellationToken cancellationToken)
    {
        // 🛠️ Fake modda her transfer "anında" on-chain doğrulanmış kabul edilir.
        return Task.FromResult<decimal?>(SimulatedActualFeeLtc);
    }

    private static string GenerateFakeAddress()
    {
        const string charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
        Span<byte> randomBytes = stackalloc byte[38];
        RandomNumberGenerator.Fill(randomBytes);
        var chars = new char[38];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = charset[randomBytes[i] % charset.Length];
        }

        return "ltc1q" + new string(chars);
    }
}
