using api.Services.Payments;

namespace api.Tests.TestSupport;

/// <summary>Retry/backoff/jitter mantığını test etmek için her zaman başarısız olan sahte provider.</summary>
public class FailingPaymentProvider : IPaymentProvider
{
    public Task<ProviderInvoice> CreateInvoiceAsync(string matchId, string playerId, decimal amountLtc, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<ProviderTransferResult> SendPayoutAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken) =>
        throw new HttpRequestException("simulated network failure");

    public Task<ProviderTransferResult> SendRefundAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken) =>
        throw new HttpRequestException("simulated network failure");

    public Task<decimal?> GetActualNetworkFeeAsync(string btcPayTransactionId, CancellationToken cancellationToken) =>
        Task.FromResult<decimal?>(null);
}
