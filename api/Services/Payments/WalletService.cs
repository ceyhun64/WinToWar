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
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        PaymentDbContext db,
        IPriceOracle priceOracle,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        ILogger<WalletService> logger)
    {
        _db = db;
        _priceOracle = priceOracle;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<decimal> GetBalanceAsync(string playerId, CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.PlayerId == playerId, cancellationToken);
        return wallet?.BalanceUsd ?? 0m;
    }

    /// <summary>Bakiyeye ekler (top-up onayı, top-up-ve-katıl shortfall onayı). Kendi SaveChanges'ını yapar.</summary>
    public async Task CreditAsync(string playerId, decimal amountUsd, CancellationToken cancellationToken)
    {
        if (amountUsd < 0)
        {
            throw new InvalidOperationException("Negatif tutar bakiyeye eklenemez.");
        }

        var wallet = await GetOrCreateWalletAsync(playerId, cancellationToken);
        wallet.BalanceUsd = PaymentMath.RoundUsdForPersistence(wallet.BalanceUsd + amountUsd);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Wallet bakiyesi arttı: {PlayerId}, +{AmountUsd} USD, yeni bakiye {Balance}", playerId, amountUsd, wallet.BalanceUsd);
    }

    /// <summary>Yeterli bakiye varsa düşer ve true döner; yoksa dokunmadan false döner. Kendi SaveChanges'ını yapar.</summary>
    public async Task<bool> TryDebitAsync(string playerId, decimal amountUsd, CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.PlayerId == playerId, cancellationToken);
        if (wallet is null || wallet.BalanceUsd < amountUsd)
        {
            return false;
        }

        wallet.BalanceUsd = PaymentMath.RoundUsdForPersistence(wallet.BalanceUsd - amountUsd);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Wallet bakiyesi düştü: {PlayerId}, -{AmountUsd} USD, yeni bakiye {Balance}", playerId, amountUsd, wallet.BalanceUsd);
        return true;
    }

    /// <summary>Bir oyuncunun "dosyadaki" (en son sağladığı) LTC ödül adresi — hiç sağlamadıysa null.</summary>
    public async Task<string?> GetPayoutAddressAsync(string playerId, CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.PlayerId == playerId, cancellationToken);
        return wallet?.PayoutAddress;
    }

    /// <summary>
    /// 🛠️ Bir oyuncu bir odaya PaymentInvoice hiç oluşturulmadan (mevcut bakiyeden
    /// doğrudan) katıldığında da kazanırsa ödülünün gönderileceği bir adres gerekir
    /// — RoomEntryService, bu metotla yeni sağlanan bir adresi kalıcı olarak
    /// kaydeder (PayoutService, invoice'ı olmayan kazananlar için buraya bakar).
    /// suppliedAddress boşsa dosyadaki mevcut adresi (varsa) döner, hiçbiri yoksa null.
    /// </summary>
    public async Task<string?> ResolveAndSavePayoutAddressAsync(string playerId, string? suppliedAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(suppliedAddress))
        {
            return await GetPayoutAddressAsync(playerId, cancellationToken);
        }

        if (!AddressValidator.TryValidate(suppliedAddress, out var format))
        {
            throw new PaymentValidationException("INVALID_PAYOUT_ADDRESS", "Geçersiz LTC adresi (checksum doğrulaması başarısız).");
        }

        var wallet = await GetOrCreateWalletAsync(playerId, cancellationToken);
        wallet.PayoutAddress = suppliedAddress;
        wallet.PayoutAddressFormat = format;
        await _db.SaveChangesAsync(cancellationToken);
        return suppliedAddress;
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
    /// docs/07-pages.md `/admin/odemeler` manuel onay. Bölüm 1.9'daki state machine'in
    /// (Pending→Approved→Sent→Completed / Rejected/Failed) dört ara/terminal durumu da
    /// bu tek çağrı içinde sırayla geçilir — Payout/Refund'daki gibi ayrı bir retry+
    /// backoff arka plan servisi bu görevde kurulmadı (🛠️ kapsam sınırlaması, raporlandı),
    /// ama bu, state'lerin kendisinin atlanmasını gerektirmez: onay anında Approved'a
    /// geçilir, BTCPay çağrısı yapılmadan hemen önce Sent'e geçilir (gönderim
    /// başlatıldığını yansıtır), sonucuna göre Completed/Failed ile sonlanır.
    /// </summary>
    public async Task<bool> ApproveWithdrawalAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await _db.WithdrawalRequests.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (request is null || request.Status != WithdrawalRequestStatus.Pending)
        {
            return false;
        }

        request.Status = WithdrawalRequestStatus.Approved;
        await _db.SaveChangesAsync(cancellationToken);

        request.Status = WithdrawalRequestStatus.Sent;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _paymentProvider.SendPayoutAsync(request.DestinationLtcAddress, request.AmountLtc, cancellationToken);
            request.Status = WithdrawalRequestStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Para çekme gönderimi başarısız: {RequestId}", request.Id);
            request.Status = WithdrawalRequestStatus.Failed;
        }

        request.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
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
        return true;
    }

    private async Task<Wallet> GetOrCreateWalletAsync(string playerId, CancellationToken cancellationToken)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.PlayerId == playerId, cancellationToken);
        if (wallet is null)
        {
            wallet = new Wallet { PlayerId = playerId, BalanceUsd = 0m };
            _db.Wallets.Add(wallet);
        }

        return wallet;
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
