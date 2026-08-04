using System.Globalization;
using api.Models.Payments;
using api.Models.Payments.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 5.3 state machine'i + retry/backoff/jitter (Bölüm 0.3) uygular.
/// İki aşamalı çalışır: <see cref="SubmitAsync"/> yalnızca bir Refund kaydı
/// EKLER (SaveChanges/commit çağıranın sorumluluğundadır — böylece PaymentService
/// gibi zaten bir transaction içinde olan çağıranlar için nested-transaction
/// sorunu oluşmaz); asıl dış çağrı (BTCPay'e gönderim) <see cref="ProcessDueRefundsAsync"/>
/// tarafından, ReconciliationService'in periyodik tick'i üzerinden ayrıca yapılır.
/// </summary>
public class RefundService
{
    private readonly PaymentDbContext _db;
    private readonly IPaymentProvider _paymentProvider;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly PaymentEventNotifier _notifier;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        PaymentDbContext db,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        PaymentEventNotifier notifier,
        ILogger<RefundService> logger)
    {
        _db = db;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// Bir invoice için refund kaydı ekler (idempotency anahtarı: PaymentInvoiceId,
    /// unique index — aynı invoice için ikinci bir SubmitAsync çağrısı, SaveChanges
    /// anında unique-violation-as-no-op ile engellenir). SaveChanges YAPMAZ.
    /// </summary>
    public async Task SubmitAsync(PaymentInvoice invoice, decimal amountLtc, RefundReason reason, CancellationToken cancellationToken)
    {
        var alreadyExists = await _db.Refunds.AnyAsync(r => r.PaymentInvoiceId == invoice.Id, cancellationToken);
        if (alreadyExists)
        {
            _logger.LogInformation("Invoice için zaten bir refund kaydı var, atlanıyor: {InvoiceId}", invoice.Id);
            return;
        }

        _db.Refunds.Add(new Refund
        {
            Id = Guid.NewGuid(),
            PaymentInvoiceId = invoice.Id,
            PlayerId = invoice.PlayerId,
            AmountLtc = amountLtc,
            Reason = reason,
            Status = RefundStatus.RefundPending,
            CreatedAt = _timeProvider.GetUtcNow()
        });
    }

    /// <summary>
    /// docs/03-game-rules.md Bölüm 1.7 (LeaveLobby) gibi tek seferlik, transaction'sız
    /// çağrılar için: bir maç+oyuncu için onaylanmış (Confirmed) en güncel invoice'ı bulur.
    /// </summary>
    public async Task<PaymentInvoice?> FindConfirmedInvoiceAsync(string matchId, string playerId, CancellationToken cancellationToken)
    {
        var invoices = await _db.PaymentInvoices
            .Where(i => i.MatchId == matchId && i.PlayerId == playerId && i.Status == PaymentInvoiceStatus.Confirmed)
            .ToListAsync(cancellationToken);
        return invoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
    }

    /// <summary>SubmitAsync + SaveChanges — kendi transaction'ı olmayan, tek seferlik çağıranlar için (bkz. GameHub.LeaveLobby).</summary>
    public async Task SubmitAndPersistAsync(PaymentInvoice invoice, decimal amountLtc, RefundReason reason, CancellationToken cancellationToken)
    {
        await SubmitAsync(invoice, amountLtc, reason, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// ReconciliationService tarafından periyodik çağrılır: gönderilmemiş veya
    /// retry zamanı gelmiş RefundPending kayıtlarını BTCPay'e gönderir.
    /// </summary>
    public async Task ProcessDueRefundsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        // 🛠️ Not: NextRetryAt (DateTimeOffset) karşılaştırması client-side yapılır —
        // SQLite EF Core provider'ı bu tür ifadeleri WHERE içinde translate edemiyor.
        var candidates = await _db.Refunds
            .Where(r => r.Status == RefundStatus.RefundPending && r.BtcPayTransactionId == null)
            .ToListAsync(cancellationToken);
        var due = candidates.Where(r => r.NextRetryAt is null || r.NextRetryAt <= now).ToList();

        foreach (var refund in due)
        {
            await TrySendAsync(refund, cancellationToken);
        }
    }

    private async Task TrySendAsync(Refund refund, CancellationToken cancellationToken)
    {
        var invoice = await _db.PaymentInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == refund.PaymentInvoiceId, cancellationToken);
        if (invoice is null)
        {
            _logger.LogError("Refund için invoice bulunamadı: {RefundId}, invoice={InvoiceId}", refund.Id, refund.PaymentInvoiceId);
            return;
        }

        try
        {
            var result = await _paymentProvider.SendRefundAsync(invoice.PayoutAddress, refund.AmountLtc, cancellationToken);
            refund.BtcPayTransactionId = result.BtcPayTransactionId;
            refund.Status = RefundStatus.RefundSent;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Refund gönderildi: {RefundId} -> {TxId}", refund.Id, result.BtcPayTransactionId);
        }
        catch (Exception ex)
        {
            await HandleSendFailureAsync(refund, ex, cancellationToken);
        }
    }

    private async Task HandleSendFailureAsync(Refund refund, Exception ex, CancellationToken cancellationToken)
    {
        refund.RetryCount += 1;
        var now = _timeProvider.GetUtcNow();

        if (refund.RetryCount > _config.RefundRetryCount)
        {
            refund.Status = RefundStatus.Failed;
            refund.NextRetryAt = null;
            _logger.LogError(ex, "Refund kalıcı olarak başarısız (retry limiti aşıldı): {RefundId}", refund.Id);
        }
        else
        {
            var jitterSeconds = Random.Shared.Next(0, Math.Max(1, _config.RefundRetryJitterSeconds));
            var backoffSeconds = _config.RefundRetryBaseDelaySeconds * Math.Pow(_config.RetryBackoffMultiplier, refund.RetryCount - 1);
            refund.NextRetryAt = now.AddSeconds(backoffSeconds + jitterSeconds);
            _logger.LogWarning(ex, "Refund gönderimi başarısız, {RetryAt} zamanında tekrar denenecek: {RefundId} (deneme {RetryCount}/{MaxRetries})",
                refund.NextRetryAt, refund.Id, refund.RetryCount, _config.RefundRetryCount);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// ReconciliationService tarafından çağrılır: RefundSent durumundaki kayıtlar
    /// için gerçekleşen fee/onay bilgisini kontrol eder ve Completed'a taşır.
    /// </summary>
    public async Task ReconcileSentRefundsAsync(CancellationToken cancellationToken)
    {
        var sent = await _db.Refunds.Where(r => r.Status == RefundStatus.RefundSent).ToListAsync(cancellationToken);
        foreach (var refund in sent)
        {
            var actualFee = await _paymentProvider.GetActualNetworkFeeAsync(refund.BtcPayTransactionId!, cancellationToken);
            if (actualFee is null)
            {
                continue; // henüz on-chain doğrulanmadı, sonraki tick'te tekrar denenir.
            }

            refund.Status = RefundStatus.Completed;
            refund.CompletedAt = _timeProvider.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);

            var invoice = await _db.PaymentInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == refund.PaymentInvoiceId, cancellationToken);
            if (invoice?.MatchId is not null)
            {
                // MatchId null (saf top-up invoice'ının iadesi) ise bildirilecek bir
                // SignalR maç grubu yoktur — oyuncu /cuzdan üzerinden bakiyesini görür.
                await _notifier.NotifyRefundCompletedAsync(invoice.MatchId, new RefundCompletedEvent
                {
                    MatchId = invoice.MatchId,
                    PlayerId = refund.PlayerId,
                    AmountLtc = refund.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
                    Reason = refund.Reason.ToString()
                }, cancellationToken);
            }

            _logger.LogInformation("Refund tamamlandı: {RefundId}", refund.Id);
        }
    }
}
