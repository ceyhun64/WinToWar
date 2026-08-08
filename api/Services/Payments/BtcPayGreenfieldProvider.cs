using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// BTCPay Server Greenfield REST API v2 entegrasyonu (docs/05-payment.md Bölüm 1.3,
/// Bölüm 3 akış diyagramları — invoice oluşturma, payout/refund gönderimi, gerçek
/// network fee sorgusu). Program.cs'te yalnızca Development dışı ortamlarda
/// kaydedilir (Development'ta <see cref="FakePaymentProvider"/> kullanılır).
///
/// 🚩 Bölüm 0.3 ön koşulu: bu sınıf gerçek bir BTCPay regtest/testnet instance'ına
/// karşı ÇALIŞTIRILARAK doğrulanamadı — erişim yok. Endpoint yolları ve JSON alan
/// adları Greenfield API v2'nin dokümante edilmiş sözleşmesine göre yazıldı; canlıya
/// alınmadan önce gerçek bir BTCPay instance'ına karşı uçtan uca test edilmelidir
/// (özellikle payment-methods yanıtındaki "destination"/"paymentLink" alan adları ve
/// wallet/transactions ucunun sürüme göre değişebilecek şekli).
/// </summary>
public class BtcPayGreenfieldProvider : IPaymentProvider
{
    /// <summary>Bölüm 1.3: proje tek para birimi olarak LTC destekler, on-chain (Lightning değil).</summary>
    private const string LtcPaymentMethodId = "LTC-CHAIN";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly PaymentConfig _config;
    private readonly ILogger<BtcPayGreenfieldProvider> _logger;

    public BtcPayGreenfieldProvider(IHttpClientFactory httpClientFactory, IOptions<PaymentConfig> config, ILogger<BtcPayGreenfieldProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("BtcPay");
        _config = config.Value;
        _logger = logger;
    }

    public async Task<ProviderInvoice> CreateInvoiceAsync(
        string? matchId, string playerId, decimal amountLtc, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var expirationMinutes = Math.Max(1, (int)Math.Ceiling((expiresAt - DateTimeOffset.UtcNow).TotalMinutes));
        var requestBody = new BtcPayCreateInvoiceRequest(
            amountLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
            "LTC",
            new BtcPayInvoiceMetadata(matchId, playerId),
            new BtcPayInvoiceCheckout(expirationMinutes, [LtcPaymentMethodId]));

        using var invoiceResponse = await _httpClient.PostAsJsonAsync(
            $"/api/v1/stores/{_config.BtcPayStoreId}/invoices", requestBody, JsonOptions, cancellationToken);
        invoiceResponse.EnsureSuccessStatusCode();

        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<BtcPayInvoiceResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("BTCPay invoice yanıtı boş.");

        var paymentMethod = await GetLtcPaymentMethodAsync(invoice.Id, cancellationToken);

        _logger.LogInformation(
            "[BtcPayGreenfieldProvider] Invoice oluşturuldu: {InvoiceId}, match={MatchId}, oyuncu={PlayerId}, tutar={AmountLtc} LTC",
            invoice.Id, matchId, playerId, amountLtc);

        return new ProviderInvoice(invoice.Id, paymentMethod.Destination, paymentMethod.PaymentLink);
    }

    private async Task<BtcPayInvoicePaymentMethod> GetLtcPaymentMethodAsync(string invoiceId, CancellationToken cancellationToken)
    {
        var methods = await _httpClient.GetFromJsonAsync<List<BtcPayInvoicePaymentMethod>>(
            $"/api/v1/stores/{_config.BtcPayStoreId}/invoices/{invoiceId}/payment-methods", JsonOptions, cancellationToken);

        return methods?.FirstOrDefault(m => m.PaymentMethod.StartsWith("LTC", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"BTCPay invoice {invoiceId} için LTC ödeme yöntemi bulunamadı.");
    }

    public Task<ProviderTransferResult> SendPayoutAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken)
        => SendOnChainTransferAsync(destinationAddress, amountLtc, cancellationToken);

    public Task<ProviderTransferResult> SendRefundAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken)
        => SendOnChainTransferAsync(destinationAddress, amountLtc, cancellationToken);

    /// <summary>
    /// BTCPay'in store hot wallet'ından doğrudan gönderim ucu — Bölüm 1.3: payout ve
    /// refund'un ikisi de aynı LTC hot wallet'tan otomatik gönderilir (ayrı bir manuel
    /// onay akışı yok), bu yüzden aynı çağrı paylaşılır.
    /// </summary>
    private async Task<ProviderTransferResult> SendOnChainTransferAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken)
    {
        var requestBody = new BtcPayCreateTransactionRequest(
            [new BtcPayTransactionDestination(destinationAddress, amountLtc.ToString("0.00000000", CultureInfo.InvariantCulture))]);

        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/stores/{_config.BtcPayStoreId}/payment-methods/{LtcPaymentMethodId}/wallet/transactions", requestBody, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var transaction = await response.Content.ReadFromJsonAsync<BtcPayTransactionResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("BTCPay transfer yanıtı boş.");

        _logger.LogInformation(
            "[BtcPayGreenfieldProvider] On-chain transfer gönderildi: {TxId}, hedef={Address}, tutar={AmountLtc} LTC",
            transaction.TransactionHash, destinationAddress, amountLtc);

        // Bölüm 2.6: "estimated" fee burada kesin değildir — gerçek (actual) fee
        // yalnızca GetActualNetworkFeeAsync ile, on-chain doğrulama sonrası okunur.
        return new ProviderTransferResult(transaction.TransactionHash, 0m);
    }

    public async Task<decimal?> GetActualNetworkFeeAsync(string btcPayTransactionId, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/api/v1/stores/{_config.BtcPayStoreId}/payment-methods/{LtcPaymentMethodId}/wallet/transactions/{btcPayTransactionId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var transaction = await response.Content.ReadFromJsonAsync<BtcPayTransactionResponse>(JsonOptions, cancellationToken);
        if (transaction is null || transaction.Confirmations < _config.RequiredConfirmations || transaction.FeeLtc is null)
        {
            return null;
        }

        return decimal.Parse(transaction.FeeLtc, CultureInfo.InvariantCulture);
    }

    private record BtcPayCreateInvoiceRequest(string Amount, string Currency, BtcPayInvoiceMetadata Metadata, BtcPayInvoiceCheckout Checkout);
    private record BtcPayInvoiceMetadata(string? MatchId, string PlayerId);
    private record BtcPayInvoiceCheckout(int ExpirationMinutes, string[] PaymentMethods);
    private record BtcPayInvoiceResponse(string Id);
    private record BtcPayInvoicePaymentMethod(
        [property: JsonPropertyName("paymentMethod")] string PaymentMethod,
        [property: JsonPropertyName("destination")] string Destination,
        [property: JsonPropertyName("paymentLink")] string PaymentLink);
    private record BtcPayCreateTransactionRequest(BtcPayTransactionDestination[] Destinations);
    private record BtcPayTransactionDestination(string Destination, string Amount);
    private record BtcPayTransactionResponse(
        [property: JsonPropertyName("transactionHash")] string TransactionHash,
        [property: JsonPropertyName("confirmations")] int Confirmations,
        [property: JsonPropertyName("fee")] string? FeeLtc);
}
