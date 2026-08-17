using api.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace api.Services.Payments;

/// <summary>
/// İade artık on-chain LTC gönderimi değil, doğrudan kazanana/oyuncuya
/// Wallet.BalanceUsd kredisi olarak işlenir (2026-08-08 kararı — bkz.
/// docs/05-payment.md Bölüm 1.9, PayoutService'teki aynı desen). Bu yüzden ayrı
/// bir gönderim/retry/reconciliation adımına ihtiyaç yoktur: <see cref="SubmitAsync"/>
/// hem Refund kaydını ekler hem de krediyi aynı anda uygular; SaveChanges/commit
/// yine de çağıranın sorumluluğundadır (böylece PaymentService gibi zaten bir
/// transaction içinde olan çağıranlar için nested-transaction sorunu oluşmaz).
/// </summary>
public class RefundService
{
    private readonly PaymentDbContext _db;
    private readonly WalletService _walletService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefundService> _logger;

    public RefundService(
        PaymentDbContext db,
        WalletService walletService,
        TimeProvider timeProvider,
        ILogger<RefundService> logger)
    {
        _db = db;
        _walletService = walletService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Bir invoice için refund kaydı ekler ve tutarı aynı anda oyuncunun bakiyesine
    /// kredi olarak işler. SaveChanges YAPMAZ — çağıranın kendi transaction'ı içinde
    /// kalıcılaşır. İnvoice tam olarak iade ediliyorsa (Confirmed → Refunded ileri
    /// geçiş geçerliyse) invoice.Status da burada güncellenir — kısmi bir fazla ödeme
    /// iadesinde (invoice hâlâ Pending iken, webhook'un kendi akışı invoice'ı ayrıca
    /// Confirmed'e taşıyacağından) bu geçiş doğal olarak uygulanmaz.
    ///
    /// 🔒 Eşzamanlılık notu: bu metot yalnızca `PaymentService.HandleWebhookAsync`
    /// içinden (overpayment) çağrılır — o metodun kendi `ProcessedWebhookEvents`
    /// INSERT-first unique constraint'i, aynı webhook event'inin eşzamanlı iki kez
    /// işlenmesini üst seviyede zaten engeller (bkz. o metodun kendi idempotency
    /// yorumu). Bu yüzden burada SaveChanges'tan önce krediyi uygulamak güvenlidir.
    /// Bağımsız/tekrar tetiklenebilir çağıranlar (ör. admin'in "manuel iade" butonu)
    /// bu metodu DEĞİL, aşağıdaki <see cref="SubmitAndPersistAsync"/>'i kullanmalı —
    /// o, aynı yarış durumuna karşı DB seviyesinde korunur.
    ///
    /// docs/16-wallet-balance-sync.md: bu metot WalletService.NotifyBalanceChangedAsync'i
    /// ÇAĞIRMAZ — SaveChanges/commit gibi bildirim de çağıranın (PaymentService)
    /// kendi transaction'ı commit olduktan sonraki sorumluluğudur.
    /// </summary>
    public async Task SubmitAsync(PaymentInvoice invoice, decimal amountUsd, RefundReason reason, CancellationToken cancellationToken)
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
            AmountUsd = amountUsd,
            Reason = reason,
            CreatedAt = _timeProvider.GetUtcNow()
        });

        await _walletService.CreditAsync(invoice.PlayerId, amountUsd, cancellationToken);

        if (StatusRankPolicy.IsForwardTransition(invoice.Status, PaymentInvoiceStatus.Refunded))
        {
            invoice.Status = PaymentInvoiceStatus.Refunded;
        }

        _logger.LogInformation("Refund işlendi: {InvoiceId}, {AmountUsd} USD, sebep={Reason}", invoice.Id, amountUsd, reason);
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

    /// <summary>
    /// Kendi transaction'ı olmayan, tekrar tetiklenebilir çağıranlar için (ör.
    /// `AdminPaymentsController.RefundInvoice` — çift tıklama/çift istek riski
    /// taşır). `SubmitAsync`'in aksine krediyi UYGULAMADAN ÖNCE Refund satırını
    /// SaveChanges ile kalıcılaştırmayı dener: aynı invoice için eşzamanlı ikinci
    /// bir çağrı burada `PaymentInvoiceId` unique index'i ihlaliyle güvenle
    /// no-op'a düşer (transaction rollback edilir, kredi HİÇ uygulanmaz) — bu,
    /// `PayoutService.ProcessPayoutAsync`'teki "SaveChanges önce, kredi sonra"
    /// deseniyle aynıdır (bkz. o metodun yorumu).
    /// </summary>
    public async Task SubmitAndPersistAsync(PaymentInvoice invoice, decimal amountUsd, RefundReason reason, CancellationToken cancellationToken)
    {
        var alreadyExists = await _db.Refunds.AnyAsync(r => r.PaymentInvoiceId == invoice.Id, cancellationToken);
        if (alreadyExists)
        {
            _logger.LogInformation("Invoice için zaten bir refund kaydı var, atlanıyor: {InvoiceId}", invoice.Id);
            return;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.Refunds.Add(new Refund
        {
            Id = Guid.NewGuid(),
            PaymentInvoiceId = invoice.Id,
            PlayerId = invoice.PlayerId,
            AmountUsd = amountUsd,
            Reason = reason,
            CreatedAt = _timeProvider.GetUtcNow()
        });

        if (StatusRankPolicy.IsForwardTransition(invoice.Status, PaymentInvoiceStatus.Refunded))
        {
            invoice.Status = PaymentInvoiceStatus.Refunded;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogInformation(ex, "Refund INSERT çakışması (eşzamanlı çift çağrı), atlanıyor: {InvoiceId}", invoice.Id);
            return;
        }

        await _walletService.CreditAsync(invoice.PlayerId, amountUsd, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // docs/16-wallet-balance-sync.md Bölüm 1 "Önemli": ancak commit'ten SONRA.
        await _walletService.NotifyBalanceChangedAsync(invoice.PlayerId, cancellationToken);

        _logger.LogInformation("Refund işlendi: {InvoiceId}, {AmountUsd} USD, sebep={Reason}", invoice.Id, amountUsd, reason);
    }
}
