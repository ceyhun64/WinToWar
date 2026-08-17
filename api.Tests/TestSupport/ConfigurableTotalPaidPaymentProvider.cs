using api.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace api.Tests.TestSupport;

/// <summary>
/// docs/15-payment-flow-verification.md: overpayment/tolerans kontrolünün artık
/// webhook payload'ı yerine IPaymentProvider.GetTotalPaidLtcAsync'ten okunduğunu
/// doğrulamak için — diğer tüm davranışı gerçek FakePaymentProvider'a devreder,
/// yalnızca bu tek metodun dönüş değeri testten kontrol edilebilir.
/// </summary>
public class ConfigurableTotalPaidPaymentProvider : IPaymentProvider
{
    private readonly FakePaymentProvider _inner = new(NullLogger<FakePaymentProvider>.Instance);

    public decimal? TotalPaidLtc { get; set; }

    public Task<ProviderInvoice> CreateInvoiceAsync(string? matchId, string playerId, decimal amountLtc, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        _inner.CreateInvoiceAsync(matchId, playerId, amountLtc, expiresAt, cancellationToken);

    public Task<ProviderTransferResult> SendPayoutAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken) =>
        _inner.SendPayoutAsync(destinationAddress, amountLtc, cancellationToken);

    public Task<ProviderTransferResult> SendRefundAsync(string destinationAddress, decimal amountLtc, CancellationToken cancellationToken) =>
        _inner.SendRefundAsync(destinationAddress, amountLtc, cancellationToken);

    public Task<decimal?> GetActualNetworkFeeAsync(string btcPayTransactionId, CancellationToken cancellationToken) =>
        _inner.GetActualNetworkFeeAsync(btcPayTransactionId, cancellationToken);

    public Task<decimal?> GetTotalPaidLtcAsync(string btcPayInvoiceId, CancellationToken cancellationToken) =>
        Task.FromResult(TotalPaidLtc);
}
