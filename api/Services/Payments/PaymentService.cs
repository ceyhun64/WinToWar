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
    private readonly WalletService _walletService;
    private readonly RoomEntryService _roomEntryService;
    private readonly Services.MatchManager _matchManager;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        PaymentDbContext db,
        IPriceOracle priceOracle,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        PaymentEventNotifier notifier,
        RefundService refundService,
        WalletService walletService,
        RoomEntryService roomEntryService,
        Services.MatchManager matchManager,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _priceOracle = priceOracle;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _refundService = refundService;
        _walletService = walletService;
        _roomEntryService = roomEntryService;
        _matchManager = matchManager;
        _logger = logger;
    }

    /// <summary>Bölüm 1.9: genel bakiye yükleme (top-up) — MatchId null, tutar kullanıcının seçtiği tutardır.</summary>
    public Task<PaymentInvoiceDto> CreateTopUpInvoiceAsync(
        string playerId, decimal amountUsd, string payoutAddress, CancellationToken cancellationToken)
    {
        if (amountUsd < _config.MinDepositUsd)
        {
            throw new PaymentValidationException("BELOW_MIN_DEPOSIT", $"Minimum yatırma tutarı {_config.MinDepositUsd} USD.");
        }

        return CreateInvoiceInternalAsync(matchId: null, playerId, playerName: null, amountUsd, payoutAddress, cancellationToken);
    }

    /// <summary>
    /// Bölüm 1.9: bakiye yetersiz olduğunda açılan "top-up-ve-katıl" invoice'ı —
    /// tutar her zaman sunucu tarafından hesaplanır (Room.EntryFeeUsd - Wallet.BalanceUsd),
    /// client'tan gelen bir tutara asla güvenilmez (sunucu otoriter olmalı).
    /// </summary>
    public async Task<PaymentInvoiceDto> CreateMatchEntryInvoiceAsync(
        string matchId, string playerId, string playerName, string payoutAddress, CancellationToken cancellationToken)
    {
        if (!_matchManager.TryGetMatch(matchId, out var match))
        {
            throw new PaymentValidationException("MATCH_NOT_FOUND", "Oda bulunamadı.");
        }

        var balance = await _walletService.GetBalanceAsync(playerId, cancellationToken);
        var shortfall = match.Room.EntryFeeUsd - balance;
        if (shortfall <= 0)
        {
            throw new PaymentValidationException("ALREADY_SUFFICIENT_BALANCE", "Bakiyeniz zaten giriş ücretine yetiyor, doğrudan katılabilirsiniz.");
        }

        return await CreateInvoiceInternalAsync(matchId, playerId, playerName, shortfall, payoutAddress, cancellationToken);
    }

    private async Task<PaymentInvoiceDto> CreateInvoiceInternalAsync(
        string? matchId, string playerId, string? playerName, decimal amountUsd, string payoutAddress, CancellationToken cancellationToken)
    {
        if (!AddressValidator.TryValidate(payoutAddress, out var addressFormat))
        {
            throw new PaymentValidationException("INVALID_PAYOUT_ADDRESS", "Geçersiz LTC adresi (checksum doğrulaması başarısız).");
        }

        if (matchId is not null)
        {
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
        }

        var quote = await _priceOracle.GetRateAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddSeconds(_config.PriceQuoteValiditySeconds);

        // Ara değer yuvarlanmaz (Bölüm 2.3); provider çağrısına da hemen aşağıda
        // tek seferde yuvarlanmış nihai değer verilir.
        var rawAmountLtc = PaymentMath.CalculateAmountLtc(amountUsd, quote.UsdPerLtc);
        var amountLtc = PaymentMath.RoundForPersistence(rawAmountLtc);

        var providerInvoice = await _paymentProvider.CreateInvoiceAsync(matchId, playerId, amountLtc, expiresAt, cancellationToken);

        var invoice = new PaymentInvoice
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            MatchId = matchId,
            PlayerName = playerName,
            BtcPayInvoiceId = providerInvoice.BtcPayInvoiceId,
            AmountUsd = PaymentMath.RoundUsdForPersistence(amountUsd),
            AmountLtc = amountLtc,
            LockedUsdPerLtc = PaymentMath.RoundForPersistence(quote.UsdPerLtc),
            PriceOracleSource = quote.Source,
            RateServedFromCache = quote.RateServedFromCache,
            RateAgeSecondsAtUse = quote.RateAgeSecondsAtUse,
            PayoutAddress = payoutAddress,
            PayoutAddressFormat = addressFormat,
            Status = PaymentInvoiceStatus.Pending,
            MatchJoinOutcome = matchId is null ? MatchJoinOutcome.NotApplicable : MatchJoinOutcome.Pending,
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

    /// <summary>docs/07-pages.md `/gecmis`: bir oyuncunun ödeme geçmişi, en yeniden eskiye.</summary>
    public async Task<List<PaymentInvoiceDto>> GetInvoiceHistoryAsync(string playerId, CancellationToken cancellationToken)
    {
        var invoices = await _db.PaymentInvoices.AsNoTracking()
            .Where(i => i.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        return invoices
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => ToDto(i, null, null))
            .ToList();
    }

    /// <summary>docs/07-pages.md `/admin/odemeler`: başarısız/süresi dolmuş invoice'lar.</summary>
    public async Task<List<PaymentInvoiceDto>> GetFailedInvoicesAsync(CancellationToken cancellationToken)
    {
        var invoices = await _db.PaymentInvoices.AsNoTracking()
            .Where(i => i.Status == PaymentInvoiceStatus.Failed || i.Status == PaymentInvoiceStatus.Expired)
            .ToListAsync(cancellationToken);

        return invoices.OrderByDescending(i => i.CreatedAt).Select(i => ToDto(i, null, null)).ToList();
    }

    /// <summary>docs/07-pages.md `/admin`: "günlük hacim" — bugün onaylanan invoice'ların toplam USD tutarı.</summary>
    public async Task<decimal> GetTodayConfirmedVolumeUsdAsync(CancellationToken cancellationToken)
    {
        var todayStartUtc = _timeProvider.GetUtcNow().Date;
        var confirmedToday = await _db.PaymentInvoices.AsNoTracking()
            .Where(i => i.Status == PaymentInvoiceStatus.Confirmed && i.ConfirmedAt != null)
            .ToListAsync(cancellationToken);

        return confirmedToday.Where(i => i.ConfirmedAt >= todayStartUtc).Sum(i => i.AmountUsd);
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

            // Bölüm 2.1: yalnızca gösterim amaçlı ilerleme sayacı — invoice henüz
            // terminal bir state'e ulaşmadıysa, gelen webhook'un bildirdiği onay
            // sayısı öncekinden yüksekse güncellenir (hiçbir hesaplamada kullanılmaz,
            // asıl eşik kontrolü aşağıda RequiredConfirmations ile ayrıca yapılır).
            if (!StatusRankPolicy.IsTerminal(invoice.Status) && payload.Confirmations > invoice.CurrentConfirmations)
            {
                invoice.CurrentConfirmations = payload.Confirmations;
            }

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

                    // Bölüm 1.9: onay anında bakiye artırılır — saf top-up'ta bu nihai
                    // adımdır; top-up-ve-katıl'da bakiyeyi tam giriş ücretine tamamlayan
                    // adımdır (asıl lobiye ekleme, DB commit'i garantiye alındıktan SONRA
                    // aşağıda ayrıca yapılır — in-memory MatchManager state'i yalnızca
                    // kalıcılaşmış bir onay üzerine değiştirilir).
                    await _walletService.CreditAsync(invoice.PlayerId, invoice.AmountUsd, cancellationToken);
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

        if (invoiceJustConfirmed && invoice is not null && invoice.MatchId is not null)
        {
            // docs/05-payment.md Bölüm 1.9: oyuncu ödeme onaylanana kadar lobiye hiç
            // eklenmemişti (top-up-ve-katıl) — bakiye az önce tam giriş ücretine
            // tamamlandı, şimdi asıl rezervasyon/debit denenir. Oda bu sırada
            // dolmuş/başlamışsa (yarış durumu) RoomEntryService bakiyeyi otomatik
            // geri ekler ve RoomFull döner — sorgu seviyesinde tek entegrasyon
            // noktası (bkz. docs/01-workflow-rules.md 0.13 modüller arası izolasyon).
            var entryResult = await _roomEntryService.TryJoinAsync(
                invoice.MatchId, invoice.PlayerId, invoice.PlayerName ?? "Oyuncu", invoice.PayoutAddress, now.UtcDateTime, cancellationToken);

            invoice.MatchJoinOutcome = entryResult.Outcome switch
            {
                RoomEntryOutcome.Joined => MatchJoinOutcome.Joined,
                _ => MatchJoinOutcome.RoomFull
            };
            await _db.SaveChangesAsync(cancellationToken);

            if (entryResult.Outcome == RoomEntryOutcome.Joined)
            {
                await _notifier.NotifyPaymentConfirmedAsync(invoice.MatchId, new PaymentConfirmedEvent
                {
                    InvoiceId = invoice.Id.ToString(),
                    MatchId = invoice.MatchId,
                    PlayerId = invoice.PlayerId,
                    AmountLtc = invoice.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture)
                }, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "Ödeme onaylandı ama oda dolu/başlamıştı, bakiye top-up'a döndü: {InvoiceId}, {MatchId}, {PlayerId}",
                    invoice.Id, invoice.MatchId, invoice.PlayerId);
            }
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

    private PaymentInvoiceDto ToDto(PaymentInvoice invoice, string? receivingAddress, string? bip21Uri) => new()
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
        CreatedAt = invoice.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        RateServedFromCache = invoice.RateServedFromCache,
        MatchJoinOutcome = invoice.MatchJoinOutcome.ToString(),
        CurrentConfirmations = invoice.CurrentConfirmations,
        RequiredConfirmations = _config.RequiredConfirmations
    };
}
