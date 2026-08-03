using System.Globalization;
using System.Text.Json;
using api.Models.Payments;
using api.Models.Payments.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 3.1 akışını uygular: invoice oluşturma, expiry senkronu, webhook
/// doğrulama + event idempotency + monotonluk kontrolü, BIP-21 üretimi.
/// Sunucu otoriterdir (Bölüm 1.3) — yalnızca doğrulanmış, daha önce işlenmemiş
/// ve state'i geriye almayan webhook event'i esas alınır.
///
/// 🛠️ Sıra netleştirmesi: Bölüm 3.1 diyagramında INSERT'in BTCPay çağrısından
/// ÖNCE yapıldığı gösterilse de, BtcPayInvoiceId (unique idempotency anahtarı)
/// yalnızca BTCPay çağrısından SONRA bilinebilir; ayrıca bir DB transaction'ını
/// bir dış HTTP çağrısı boyunca açık tutmak yanlış olur. Bu yüzden sıra: kur al →
/// adres doğrula → BTCPay'den invoice iste → PaymentMath.RoundForPersistence ile
/// TEK SEFERDE yuvarla → atomic INSERT. Yuvarlamanın "yalnızca kalıcılaştırma
/// sınırında, bir kez" kuralı (Bölüm 2.3) bozulmuyor.
/// </summary>
public class PaymentService
{
    private readonly PaymentDbContext _db;
    private readonly IPriceOracle _priceOracle;
    private readonly IPaymentProvider _paymentProvider;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly PaymentEventNotifier _notifier;
    private readonly RefundService _refundService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentDbContext db,
        IPriceOracle priceOracle,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        PaymentEventNotifier notifier,
        RefundService refundService,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _priceOracle = priceOracle;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _refundService = refundService;
        _logger = logger;
    }

    public async Task<PaymentInvoiceDto> CreateInvoiceAsync(
        string matchId, string playerId, string payoutAddress, CancellationToken cancellationToken)
    {
        if (!AddressValidator.TryValidate(payoutAddress, out var addressFormat))
        {
            throw new PaymentValidationException("INVALID_PAYOUT_ADDRESS", "Geçersiz LTC adresi (checksum doğrulaması başarısız).");
        }

        // İdempotent oluşturma: aynı maç+oyuncu için zaten bekleyen/onaylanmış bir invoice varsa onu döndür.
        // 🛠️ Not: sıralama client-side yapılır — SQLite EF Core provider'ı ORDER BY
        // içinde DateTimeOffset ifadelerini desteklemiyor (server-side translation kısıtı).
        var existing = (await _db.PaymentInvoices
                .Where(i => i.MatchId == matchId && i.PlayerId == playerId &&
                            (i.Status == PaymentInvoiceStatus.Pending || i.Status == PaymentInvoiceStatus.Confirmed))
                .ToListAsync(cancellationToken))
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefault();

        if (existing is not null && existing.Status == PaymentInvoiceStatus.Pending && existing.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            existing = null; // süresi dolmuş, yenisi oluşturulacak
        }

        if (existing is not null)
        {
            _logger.LogInformation("Mevcut invoice yeniden döndürülüyor: {InvoiceId}", existing.Id);
            return ToDto(existing, null, null);
        }

        var quote = await _priceOracle.GetRateAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(_config.PriceQuoteValiditySeconds);

        // Ara değer yuvarlanmaz (Bölüm 2.3); provider çağrısına da hemen aşağıda
        // tek seferde yuvarlanmış nihai değer verilir.
        var rawAmountLtc = PaymentMath.CalculateAmountLtc(_config.EntryFeeUsd, quote.UsdPerLtc);
        var amountLtc = PaymentMath.RoundForPersistence(rawAmountLtc);

        var providerInvoice = await _paymentProvider.CreateInvoiceAsync(matchId, playerId, amountLtc, expiresAt, cancellationToken);

        var invoice = new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            MatchId = matchId,
            BtcPayInvoiceId = providerInvoice.BtcPayInvoiceId,
            AmountUsd = PaymentMath.RoundUsdForPersistence(_config.EntryFeeUsd),
            AmountLtc = amountLtc,
            LockedUsdPerLtc = PaymentMath.RoundForPersistence(quote.UsdPerLtc),
            PriceOracleSource = quote.Source,
            RateServedFromCache = quote.RateServedFromCache,
            RateAgeSecondsAtUse = quote.RateAgeSecondsAtUse,
            PayoutAddress = payoutAddress,
            PayoutAddressFormat = addressFormat,
            Status = PaymentInvoiceStatus.Pending,
            ExpiresAt = expiresAt,
            CreatedAt = now
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        _db.PaymentInvoices.Add(invoice);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Bölüm 8.3: unique-violation-as-no-op (BtcPayInvoiceId çakışması — aynı
            // isteğin eşzamanlı ikinci bir denemesi).
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogInformation(ex, "Invoice INSERT çakışması, mevcut kayıt kullanılacak: {BtcPayInvoiceId}", providerInvoice.BtcPayInvoiceId);
            var raced = await _db.PaymentInvoices.AsNoTracking()
                .FirstAsync(i => i.BtcPayInvoiceId == providerInvoice.BtcPayInvoiceId, cancellationToken);
            return ToDto(raced, providerInvoice.ReceivingAddress, providerInvoice.Bip21Uri);
        }

        _logger.LogInformation(
            "Invoice oluşturuldu: {InvoiceId} (BtcPay={BtcPayInvoiceId}), maç={MatchId}, oyuncu={PlayerId}, {AmountLtc} LTC",
            invoice.Id, invoice.BtcPayInvoiceId, matchId, playerId, amountLtc);

        return ToDto(invoice, providerInvoice.ReceivingAddress, providerInvoice.Bip21Uri);
    }

    public async Task<PaymentInvoiceDto> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _db.PaymentInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new PaymentInvoiceNotFoundException(invoiceId.ToString());
        return ToDto(invoice, null, null);
    }

    /// <summary>
    /// Bölüm 3.1 webhook akışı: imza → replay (WebhookMaxAgeSeconds) → event
    /// idempotency (Bölüm 8.4, INSERT-first — unique constraint asıl garanti) →
    /// invoice lookup → MONOTONLUK KONTROLÜ (Bölüm 5.4) → tolerans/confirmation
    /// kontrolü → tek seferlik state geçişi + SaveChanges + Commit.
    /// </summary>
    public async Task HandleWebhookAsync(string rawPayload, string? signatureHeaderValue, CancellationToken cancellationToken)
    {
        if (!WebhookSignatureValidator.IsValid(rawPayload, signatureHeaderValue, _config.WebhookSecret))
        {
            throw new PaymentValidationException("INVALID_SIGNATURE", "Webhook imzası doğrulanamadı.");
        }

        BtcPayWebhookPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<BtcPayWebhookPayload>(rawPayload)
                ?? throw new PaymentValidationException("INVALID_PAYLOAD", "Webhook payload'ı boş.");
        }
        catch (JsonException ex)
        {
            throw new PaymentValidationException("INVALID_PAYLOAD", "Webhook payload'ı çözümlenemedi: " + ex.Message);
        }

        var now = _timeProvider.GetUtcNow();
        var eventTime = DateTimeOffset.FromUnixTimeSeconds(payload.Timestamp);
        if ((now - eventTime).TotalSeconds > _config.WebhookMaxAgeSeconds)
        {
            _logger.LogWarning("Webhook reddedildi (replay/çok eski): {EventId}, age={AgeSeconds}s", payload.DeliveryId, (now - eventTime).TotalSeconds);
            throw new PaymentValidationException("WEBHOOK_TOO_OLD", "Webhook çok eski, replay koruması devrede.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

        // İdempotency: EventId INSERT'i en başta denenir — asıl garanti unique
        // constraint'tir (bir "var mı?" ön kontrolü değil, çünkü o TOCTOU race'e açıktır).
        var processedEvent = new ProcessedWebhookEvent { EventId = payload.DeliveryId, ProcessedAt = now, PaymentInvoiceId = null };
        _db.ProcessedWebhookEvents.Add(processedEvent);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // Rollback yalnızca DB'yi geri alır; EF Core change tracker'ı otomatik
            // temizlemez. Aynı DbContext örneği (ör. bir hosted service döngüsünde
            // veya bu testte olduğu gibi) sonraki bir işlemde tekrar kullanılabileceğinden,
            // başarısız Add'i tracker'dan da kaldırıyoruz.
            _db.Entry(processedEvent).State = EntityState.Detached;
            _logger.LogInformation("Webhook zaten işlenmiş (idempotent no-op): {EventId}", payload.DeliveryId);
            return;
        }

        var invoice = await _db.PaymentInvoices.FirstOrDefaultAsync(i => i.BtcPayInvoiceId == payload.InvoiceId, cancellationToken);
        var invoiceJustConfirmed = false;

        if (invoice is null)
        {
            _logger.LogWarning("Webhook bilinmeyen invoice için geldi: {BtcPayInvoiceId}", payload.InvoiceId);
        }
        else
        {
            processedEvent.PaymentInvoiceId = invoice.Id;
            var incomingStatus = MapEventTypeToStatus(payload.Type);

            if (incomingStatus is null)
            {
                _logger.LogInformation("Bilgi amaçlı webhook, state değişmedi: {EventType}, invoice={InvoiceId}", payload.Type, invoice.Id);
            }
            else if (!StatusRankPolicy.IsForwardTransition(invoice.Status, incomingStatus.Value))
            {
                _logger.LogInformation(
                    "Stale/out-of-order webhook ignored, current={Current} incoming={Incoming}, invoice={InvoiceId}",
                    invoice.Status, incomingStatus.Value, invoice.Id);
            }
            else if (incomingStatus.Value == PaymentInvoiceStatus.Confirmed && payload.Confirmations < _config.RequiredConfirmations)
            {
                _logger.LogInformation(
                    "Confirmation eşiği henüz sağlanmadı: {Confirmations}/{Required}, invoice={InvoiceId}",
                    payload.Confirmations, _config.RequiredConfirmations, invoice.Id);
            }
            else
            {
                if (incomingStatus.Value == PaymentInvoiceStatus.Confirmed)
                {
                    await ApplyToleranceAndOverpaymentAsync(invoice, payload, cancellationToken);
                    invoice.ConfirmedAt = now;
                    invoiceJustConfirmed = true;
                }

                invoice.Status = incomingStatus.Value;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Bkz. yukarıdaki DbUpdateException catch bloğundaki not: aynı DbContext
        // örneği tekrar kullanılabileceğinden (hosted service, test vb.), başarıyla
        // kalıcılaşmış olsa da entity'yi tracker'dan çıkarıyoruz — aksi halde aynı
        // EventId'yle gelecek bambaşka bir event/entity için Add() "zaten izleniyor"
        // hatası verir.
        _db.Entry(processedEvent).State = EntityState.Detached;

        if (invoiceJustConfirmed && invoice is not null)
        {
            await _notifier.NotifyPaymentConfirmedAsync(invoice.MatchId, new PaymentConfirmedEvent
            {
                InvoiceId = invoice.Id.ToString(),
                MatchId = invoice.MatchId,
                PlayerId = invoice.PlayerId,
                AmountLtc = invoice.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture)
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Bölüm 1.2: RefundOverpaymentThresholdUsd, invoice'ın kilitlediği
    /// LockedUsdPerLtc üzerinden LTC'ye çevrilerek karşılaştırılır. Eşiği aşan bir
    /// fazla ödeme varsa fazlalık için Refund kaydı açılır (gönderim, çağıranın
    /// SaveChanges'ı ile birlikte aynı transaction'da kalıcılaşır; asıl gönderim
    /// RefundService.ProcessDueRefundsAsync tarafından ayrıca yapılır).
    /// </summary>
    private async Task ApplyToleranceAndOverpaymentAsync(PaymentInvoice invoice, BtcPayWebhookPayload payload, CancellationToken cancellationToken)
    {
        if (payload.PaidAmountLtc is null)
        {
            return;
        }

        var paidLtc = decimal.Parse(payload.PaidAmountLtc, NumberStyles.Number, CultureInfo.InvariantCulture);
        var toleranceLtc = invoice.AmountLtc * _config.PaymentToleranceRate;
        var overpaymentLtc = paidLtc - invoice.AmountLtc - toleranceLtc;

        if (overpaymentLtc <= 0)
        {
            return;
        }

        var thresholdLtc = _config.RefundOverpaymentThresholdUsd / invoice.LockedUsdPerLtc;
        if (overpaymentLtc <= thresholdLtc)
        {
            return;
        }

        var refundAmountLtc = PaymentMath.RoundForPersistence(overpaymentLtc);
        await _refundService.SubmitAsync(invoice, refundAmountLtc, RefundReason.Overpayment, cancellationToken);
    }

    private static PaymentInvoiceStatus? MapEventTypeToStatus(string eventType) => eventType switch
    {
        BtcPayWebhookEventTypes.InvoiceSettled => PaymentInvoiceStatus.Confirmed,
        BtcPayWebhookEventTypes.InvoiceExpired => PaymentInvoiceStatus.Expired,
        BtcPayWebhookEventTypes.InvoiceInvalid => PaymentInvoiceStatus.Failed,
        _ => null
    };

    private static PaymentInvoiceDto ToDto(PaymentInvoice invoice, string? receivingAddress, string? bip21Uri) => new()
    {
        InvoiceId = invoice.Id.ToString(),
        MatchId = invoice.MatchId,
        PlayerId = invoice.PlayerId,
        Status = invoice.Status.ToString(),
        AmountUsd = invoice.AmountUsd.ToString("0.00", CultureInfo.InvariantCulture),
        AmountLtc = invoice.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
        LockedUsdPerLtc = invoice.LockedUsdPerLtc.ToString("0.00000000", CultureInfo.InvariantCulture),
        ReceivingAddress = receivingAddress ?? string.Empty,
        Bip21Uri = bip21Uri ?? string.Empty,
        ExpiresAt = invoice.ExpiresAt.ToString("O", CultureInfo.InvariantCulture),
        RateServedFromCache = invoice.RateServedFromCache
    };
}
