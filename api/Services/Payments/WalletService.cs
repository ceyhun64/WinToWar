using api.Models.Payments;
using api.Models.Payments.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 1.9: oyuncu bakiyesi (Wallet) ve para çekme talepleri (WithdrawalRequest).
/// PaymentService'ten ayrı tutulur çünkü PaymentService BTCPay invoice/webhook
/// orkestrasyonuna odaklıdır; bakiye artırma/azaltma ise burada, tek bir yerden
/// (guard'lı) yapılır — negatif bakiye asla oluşmaz (ayrıca PaymentDbContext'te
/// son savunma hattı olarak da korunur). Bölüm 0.3: zaman her yerde TimeProvider
/// üzerinden alınır, DateTime.UtcNow doğrudan çağrılmaz.
/// </summary>
public class WalletService
{
    private readonly PaymentDbContext _db;
    private readonly IPriceOracle _priceOracle;
    private readonly IPaymentProvider _paymentProvider;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly PaymentEventNotifier _notifier;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        PaymentDbContext db,
        IPriceOracle priceOracle,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        PaymentEventNotifier notifier,
        ILogger<WalletService> logger)
    {
        _db = db;
        _priceOracle = priceOracle;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// docs/16-wallet-balance-sync.md Bölüm 1: bakiye değiştiren her akış (top-up,
    /// refund, oda giriş ücreti/iadesi, maç ödülü, çekim talebi/reddi) bu metodu
    /// KENDİ commit noktasından SONRA çağırır. CreditAsync/TryDebitAsync'in içine
    /// gömülmedi çünkü bu ikisi bazı çağıranlarda (PaymentService, PayoutService,
    /// RefundService.SubmitAndPersistAsync) ambient bir transaction içinde
    /// çalışıyor — içeriden bildirim göndermek "commit'ten SONRA yayınla" kuralını
    /// ihlal ederdi (rollback olabilecek bir değeri erken yayınlamış olurduk).
    /// DTO oluşturma + yayın burada tek yerde toplanır, tetikleme her çağıranın
    /// kendi commit noktasında kalır.
    /// </summary>
    public async Task NotifyBalanceChangedAsync(string playerId, CancellationToken cancellationToken)
    {
        var balance = await GetBalanceDtoAsync(playerId, cancellationToken);
        await _notifier.NotifyWalletBalanceChangedAsync(playerId, balance, cancellationToken);
    }

    public async Task<decimal> GetBalanceAsync(string playerId, CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.PlayerId == playerId, cancellationToken);
        return wallet?.BalanceUsd ?? 0m;
    }

    /// <summary>
    /// docs/02-architecture.md "Mapping tek yerde yapılır": WalletController bu
    /// DTO'yu önceden elle inşa ediyordu (docs/09-eksik-tarama-promptu.md denetimi,
    /// Faz 8'de düzeltildi) — oyun motorundaki MatchStateMapper deseniyle tutarlı
    /// olarak mapping Service katmanına taşındı.
    /// </summary>
    public async Task<WalletDto> GetBalanceDtoAsync(string playerId, CancellationToken cancellationToken)
    {
        var balance = await GetBalanceAsync(playerId, cancellationToken);
        return new WalletDto { PlayerId = playerId, BalanceUsd = balance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) };
    }

    /// <summary>
    /// Bakiyeye ekler (top-up onayı, top-up-ve-katıl shortfall onayı).
    /// 🔒 06-coding-standards.md "Thread Safety/Concurrency": eskiden oku→değiştir→yaz
    /// (SaveChanges) yapıyordu — iki eşzamanlı istek aynı okunan değeri baz alıp
    /// birbirinin yazdığını ezebiliyordu (lost update). Artık `ExecuteUpdateAsync`
    /// ile PostgreSQL'de TEK atomik `UPDATE ... SET "BalanceUsd" = "BalanceUsd" + @x`
    /// çalışır — okuma penceresi hiç yoktur, satır kilidini veritabanı kendi verir.
    /// </summary>
    public async Task CreditAsync(string playerId, decimal amountUsd, CancellationToken cancellationToken)
    {
        if (amountUsd < 0)
        {
            throw new InvalidOperationException("Negatif tutar bakiyeye eklenemez.");
        }

        var rounded = PaymentMath.RoundUsdForPersistence(amountUsd);
        await EnsureWalletRowExistsAsync(playerId, cancellationToken);

        await _db.Wallets
            .Where(w => w.PlayerId == playerId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.BalanceUsd, w => w.BalanceUsd + rounded), cancellationToken);

        _logger.LogInformation("Wallet bakiyesi arttı: {PlayerId}, +{AmountUsd} USD", playerId, rounded);
    }

    /// <summary>
    /// Yeterli bakiye varsa atomik olarak düşer ve true döner; yoksa dokunmadan
    /// false döner. 🔒 Bakiye kontrolü (`BalanceUsd >= amount`) ve azaltma AYNI
    /// SQL UPDATE'in WHERE/SET'inde birlikte çalışır — bu yüzden negatif bakiye
    /// oluşturacak bir yarış durumu (iki eşzamanlı çekim/harcama) yapısal olarak
    /// imkânsızdır (bkz. CreditAsync'teki aynı gerekçe).
    /// </summary>
    public async Task<bool> TryDebitAsync(string playerId, decimal amountUsd, CancellationToken cancellationToken)
    {
        var rounded = PaymentMath.RoundUsdForPersistence(amountUsd);

        var updatedRows = await _db.Wallets
            .Where(w => w.PlayerId == playerId && w.BalanceUsd >= rounded)
            .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.BalanceUsd, w => w.BalanceUsd - rounded), cancellationToken);

        if (updatedRows == 0)
        {
            return false;
        }

        _logger.LogInformation("Wallet bakiyesi düştü: {PlayerId}, -{AmountUsd} USD", playerId, rounded);
        return true;
    }

    /// <summary>
    /// Bölüm 1.9 "Yeni entity — WithdrawalRequest": talep anında bakiyeden düşülür
    /// (çift harcamayı önlemek için); gerçek on-chain gönderim/admin onayı bu
    /// görevin kapsamı dışında bırakıldı (ayrıca rapor edildi) — bu metot yalnızca
    /// Pending bir talep oluşturur.
    /// </summary>
    public async Task<WithdrawalRequestDto> RequestWithdrawalAsync(
        string playerId, decimal amountUsd, string destinationLtcAddress, CancellationToken cancellationToken)
    {
        if (!AddressValidator.TryValidate(destinationLtcAddress, out _))
        {
            throw new PaymentValidationException("INVALID_PAYOUT_ADDRESS", "Geçersiz LTC adresi (checksum doğrulaması başarısız).");
        }

        if (amountUsd < _config.MinWithdrawalUsd)
        {
            throw new PaymentValidationException("BELOW_MIN_WITHDRAWAL", $"Minimum çekim tutarı {_config.MinWithdrawalUsd} USD.");
        }

        var debited = await TryDebitAsync(playerId, amountUsd, cancellationToken);
        if (!debited)
        {
            throw new PaymentValidationException("INSUFFICIENT_BALANCE", "Yetersiz bakiye.");
        }

        await NotifyBalanceChangedAsync(playerId, cancellationToken);

        var quote = await _priceOracle.GetRateAsync(cancellationToken);
        var amountLtc = PaymentMath.RoundForPersistence(PaymentMath.CalculateAmountLtc(amountUsd, quote.UsdPerLtc));

        var request = new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            AmountUsd = PaymentMath.RoundUsdForPersistence(amountUsd),
            AmountLtc = amountLtc,
            DestinationLtcAddress = destinationLtcAddress,
            Status = WithdrawalRequestStatus.Pending,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _db.WithdrawalRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Para çekme talebi oluşturuldu: {RequestId}, {PlayerId}, {AmountUsd} USD", request.Id, playerId, amountUsd);

        return ToDto(request);
    }

    /// <summary>docs/07-pages.md `/admin`: özet metriklerdeki "bekleyen çekim sayısı".</summary>
    public async Task<int> CountPendingWithdrawalsAsync(CancellationToken cancellationToken)
    {
        return await _db.WithdrawalRequests.CountAsync(w => w.Status == WithdrawalRequestStatus.Pending, cancellationToken);
    }

    /// <summary>docs/07-pages.md `/admin/odemeler`: bekleyen para çekme talepleri.</summary>
    public async Task<List<WithdrawalRequestDto>> ListPendingWithdrawalsAsync(CancellationToken cancellationToken)
    {
        var pending = await _db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.Status == WithdrawalRequestStatus.Pending)
            .ToListAsync(cancellationToken);

        return pending.OrderBy(w => w.CreatedAt).Select(ToDto).ToList();
    }

    /// <summary>
    /// docs/08-page-content.md Bölüm 3.9 "Bekleyen Transferler" kartı: oyuncunun
    /// henüz sonuçlanmamış çekim talepleri. 🛠️ Metnin literal ifadesi "henüz
    /// Completed olmayan" der, ama kart adı ve gerekçesi ("param nerede"
    /// belirsizliği) yalnızca devam eden (Pending/Approved/Sent) talepleri
    /// kastediyor — Rejected/Failed zaten sonuçlanmış, artık "bekleyen" değil.
    /// </summary>
    public async Task<List<WithdrawalRequestDto>> ListForPlayerAsync(string playerId, CancellationToken cancellationToken)
    {
        var pending = await _db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.PlayerId == playerId && (
                w.Status == WithdrawalRequestStatus.Pending ||
                w.Status == WithdrawalRequestStatus.Approved ||
                w.Status == WithdrawalRequestStatus.Sent))
            .ToListAsync(cancellationToken);

        return pending.OrderByDescending(w => w.CreatedAt).Select(ToDto).ToList();
    }

    /// <summary>
    /// docs/17-withdrawal-address-suggestions.md Bölüm 2: "Son kullanılan adresler"
    /// önerisi. 🛠️ Durum filtresi: yalnızca <see cref="WithdrawalRequestStatus.Completed"/>
    /// "fiilen zincire gönderildi" sayılır. <see cref="WithdrawalRequestStatus.Sent"/>
    /// bu görev için YETERSİZ — ApproveWithdrawalAsync'e bakıldığında Sent, gerçek
    /// `SendPayoutAsync` çağrısından ÖNCE (Bölüm "gönderim başlatıldığını yansıtır"
    /// yorumuyla) commit edilir; yani bir talep Sent'te kalıp hiç Completed'a
    /// geçmeden süreç çökebilir (nadir ama mümkün) — bu durumda adres aslında
    /// zincire gitmemiş/gitmediği belirsiz olabilir. Completed ise SendPayoutAsync
    /// başarıyla döndükten SONRA yazılır, yani "gerçekten gönderildi" garantisini
    /// veren tek terminal durum budur.
    /// </summary>
    public async Task<List<WithdrawalAddressSuggestionDto>> GetWithdrawalAddressSuggestionsAsync(
        string playerId, CancellationToken cancellationToken)
    {
        var sent = await _db.WithdrawalRequests.AsNoTracking()
            .Where(w => w.PlayerId == playerId && w.Status == WithdrawalRequestStatus.Completed)
            .ToListAsync(cancellationToken);

        return sent
            .GroupBy(w => w.DestinationLtcAddress)
            .Select(g => new { Address = g.Key, LastUsedAt = g.Max(w => w.CreatedAt) })
            .OrderByDescending(g => g.LastUsedAt)
            .Take(5)
            .Select(g => new WithdrawalAddressSuggestionDto
            {
                Address = g.Address,
                LastUsedAt = g.LastUsedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            })
            .ToList();
    }

    /// <summary>
    /// docs/07-pages.md `/admin/odemeler` manuel onay. Bölüm 1.9'daki state machine'in
    /// (Pending→Approved→Sent→Completed / Rejected/Failed) dört ara/terminal durumu da
    /// bu tek çağrı içinde sırayla geçilir — Payout/Refund'daki gibi ayrı bir retry+
    /// backoff arka plan servisi bu görevde kurulmadı (🛠️ kapsam sınırlaması, raporlandı),
    /// ama bu, state'lerin kendisinin atlanmasını gerektirmez: onay anında Approved'a
    /// geçilir, BTCPay çağrısı yapılmadan hemen önce Sent'e geçilir (gönderim
    /// başlatıldığını yansıtır), sonucuna göre Completed/Failed ile sonlanır.
    ///
    /// 🔒 docs/15-payment-flow-verification.md eşzamanlılık bulgusu: önceki sürüm
    /// oku→kontrol et→yaz (check-then-act) yapıyordu — aynı ID'ye eşzamanlı iki admin
    /// onay isteği (çift tıklama/iki sekme) ikisi de "Status==Pending" kontrolünü
    /// UPDATE'ten önce geçebilir, bu da BTCPay'e ÇİFT on-chain gönderim (gerçek para
    /// kaybı) anlamına gelirdi (bkz. 06-coding-standards.md "Thread Safety/Concurrency" —
    /// "iki eşzamanlı akışın aynı state'e korumasız yazması asla olmaz"). Düzeltme:
    /// Pending→Approved geçişi artık WalletService.TryDebitAsync'teki ile aynı desende,
    /// tek atomik `UPDATE ... WHERE Status = 'Pending'` ile yapılır — PostgreSQL bu
    /// satırı UPDATE sırasında kilitler, eşzamanlı ikinci bir UPDATE her zaman 0 satır
    /// günceller (WHERE'i artık sağlamaz) ve güvenle false döner; hiçbir okuma penceresi
    /// yoktur.
    /// </summary>
    public async Task<bool> ApproveWithdrawalAsync(Guid id, CancellationToken cancellationToken)
    {
        var claimed = await _db.WithdrawalRequests
            .Where(w => w.Id == id && w.Status == WithdrawalRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.Status, WithdrawalRequestStatus.Approved), cancellationToken);

        if (claimed == 0)
        {
            return false;
        }

        var request = await _db.WithdrawalRequests.FirstAsync(w => w.Id == id, cancellationToken);

        request.Status = WithdrawalRequestStatus.Sent;
        await _db.SaveChangesAsync(cancellationToken);

        var sendFailed = false;
        try
        {
            await _paymentProvider.SendPayoutAsync(request.DestinationLtcAddress, request.AmountLtc, cancellationToken);
            request.Status = WithdrawalRequestStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Para çekme gönderimi başarısız: {RequestId}", request.Id);
            request.Status = WithdrawalRequestStatus.Failed;
            sendFailed = true;
        }

        request.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);

        // 🐞 docs/21-payment-sandbox-e2e.md Aşama 5 (Bölüm 7, adım 6) gerçek regtest
        // bulgusu: gönderim başarısız olduğunda (sandbox'ta BTCPay'in hot wallet'ında
        // yeterli LTC olmadığı için gelen 422 ile fiilen üretildi) talep `Failed`'a
        // geçiyor ama talep anında düşülen tutar oyuncunun bakiyesine GERİ
        // EKLENMİYORDU — para sessizce kayboluyordu. docs/05-payment.md Bölüm 1.9
        // bunu açıkça kural olarak koyar ("`Failed`/`Rejected` durumunda bakiyeye
        // geri eklenir", ayrıca Bölüm 11 kabul kriteri) ve RejectWithdrawalAsync
        // zaten bunu yapıyordu; eksik olan yalnızca bu yoldu.
        //
        // ❓ Kalan belirsizlik (müşteriye raporlandı): gönderim on-chain'e
        // yayınlandıktan SONRA bir hata oluşursa (ör. yanıt okunurken zaman aşımı)
        // bu iade, zincire çıkmış bir ödemeyle birlikte oluşabilir. Bu ayrımı
        // güvenilir şekilde yapacak bir sinyal yok (v10'dan beri ayrı bir
        // reconciliation altyapısı da yok); kural metni "Failed → iade" dediği için
        // birebir uygulandı, ayrım gerekirse ayrı bir mutabakat kararı gerekir.
        if (sendFailed)
        {
            await CreditAsync(request.PlayerId, request.AmountUsd, cancellationToken);
            await NotifyBalanceChangedAsync(request.PlayerId, cancellationToken);
        }

        return true;
    }

    /// <summary>Reddedilen talebin tutarı bakiyeye geri eklenir (bkz. Bölüm 1.9).</summary>
    public async Task<bool> RejectWithdrawalAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await _db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (request is null || request.Status != WithdrawalRequestStatus.Pending)
        {
            return false;
        }

        request.Status = WithdrawalRequestStatus.Rejected;
        request.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);

        await CreditAsync(request.PlayerId, request.AmountUsd, cancellationToken);
        await NotifyBalanceChangedAsync(request.PlayerId, cancellationToken);
        return true;
    }

    /// <summary>
    /// CreditAsync/TryDebitAsync'in atomik `ExecuteUpdateAsync` yolu yalnızca VAR
    /// olan satırları günceller, yeni satır oluşturmaz — bu yüzden ilk kredi öncesi
    /// satırın var olduğu garanti edilir. İki eşzamanlı isteğin aynı yeni playerId
    /// için satırı aynı anda oluşturmaya çalışması mümkündür; bu durumda ikincisi
    /// PK ihlaliyle karşılaşır — bu güvenle yutulur, çünkü anlamı "satır zaten var".
    /// </summary>
    private async Task EnsureWalletRowExistsAsync(string playerId, CancellationToken cancellationToken)
    {
        var exists = await _db.Wallets.AsNoTracking().AnyAsync(w => w.PlayerId == playerId, cancellationToken);
        if (exists)
        {
            return;
        }

        var entry = _db.Wallets.Add(new Wallet { PlayerId = playerId, BalanceUsd = 0m });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            entry.State = EntityState.Detached;
        }
    }

    private static WithdrawalRequestDto ToDto(WithdrawalRequest request) => new()
    {
        Id = request.Id.ToString(),
        PlayerId = request.PlayerId,
        AmountUsd = request.AmountUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
        AmountLtc = request.AmountLtc.ToString("0.00000000", System.Globalization.CultureInfo.InvariantCulture),
        DestinationLtcAddress = request.DestinationLtcAddress,
        Status = request.Status.ToString(),
        CreatedAt = request.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
    };
}
