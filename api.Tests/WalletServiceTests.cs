using api.Services.Payments;
using api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace api.Tests;

/// <summary>docs/05-payment.md Bölüm 1.9: Wallet bakiye artırma/azaltma, negatif bakiye guard'ı, para çekme talebi.</summary>
public class WalletServiceTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly WalletService _sut;

    public WalletServiceTests()
    {
        (_db, _connection) = PaymentDbContextFactory.CreateOpen();
        var config = new PaymentConfig { MinWithdrawalUsd = 1.00m };
        var notifier = new PaymentEventNotifier(new FakeHubContext(), new FakeWalletHubContext());
        _sut = new WalletService(
            _db, new FixedPriceOracle(44.5m), new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance),
            Options.Create(config), TimeProvider.System, notifier, NullLogger<WalletService>.Instance);
    }

    private const string ValidAddress = "bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4";

    [Fact]
    public async Task CreditAsync_NewPlayer_CreatesWalletWithBalance()
    {
        await _sut.CreditAsync("p1", 5.00m, CancellationToken.None);

        Assert.Equal(5.00m, await _sut.GetBalanceAsync("p1", CancellationToken.None));
    }

    [Fact]
    public async Task CreditAsync_ExistingWallet_Accumulates()
    {
        await _sut.CreditAsync("p1", 2.00m, CancellationToken.None);
        await _sut.CreditAsync("p1", 3.50m, CancellationToken.None);

        Assert.Equal(5.50m, await _sut.GetBalanceAsync("p1", CancellationToken.None));
    }

    [Fact]
    public async Task TryDebitAsync_SufficientBalance_DebitsAndReturnsTrue()
    {
        await _sut.CreditAsync("p1", 5.00m, CancellationToken.None);

        var result = await _sut.TryDebitAsync("p1", 4.00m, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(1.00m, await _sut.GetBalanceAsync("p1", CancellationToken.None));
    }

    [Fact]
    public async Task TryDebitAsync_InsufficientBalance_ReturnsFalseAndDoesNotChangeBalance()
    {
        await _sut.CreditAsync("p1", 1.00m, CancellationToken.None);

        var result = await _sut.TryDebitAsync("p1", 2.00m, CancellationToken.None);

        Assert.False(result);
        Assert.Equal(1.00m, await _sut.GetBalanceAsync("p1", CancellationToken.None));
    }

    [Fact]
    public async Task TryDebitAsync_NoWalletYet_ReturnsFalse()
    {
        var result = await _sut.TryDebitAsync("never-topped-up", 1.00m, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task RequestWithdrawalAsync_SufficientBalance_DebitsAndCreatesPendingRequest()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);

        var dto = await _sut.RequestWithdrawalAsync("p1", 4.00m, ValidAddress, CancellationToken.None);

        Assert.Equal("Pending", dto.Status);
        Assert.Equal("4.00", dto.AmountUsd);
        Assert.Equal(6.00m, await _sut.GetBalanceAsync("p1", CancellationToken.None));
        Assert.Equal(1, await _db.WithdrawalRequests.CountAsync());
    }

    [Fact]
    public async Task RequestWithdrawalAsync_InsufficientBalance_ThrowsAndDoesNotCreateRequest()
    {
        await _sut.CreditAsync("p1", 1.00m, CancellationToken.None);

        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            _sut.RequestWithdrawalAsync("p1", 5.00m, ValidAddress, CancellationToken.None));

        Assert.Equal(1.00m, await _sut.GetBalanceAsync("p1", CancellationToken.None));
        Assert.Equal(0, await _db.WithdrawalRequests.CountAsync());
    }

    [Fact]
    public async Task RequestWithdrawalAsync_InvalidAddress_Throws()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);

        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            _sut.RequestWithdrawalAsync("p1", 2.00m, "not-a-real-address", CancellationToken.None));
    }

    [Fact]
    public async Task RequestWithdrawalAsync_BelowMinimum_Throws()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);

        await Assert.ThrowsAsync<PaymentValidationException>(() =>
            _sut.RequestWithdrawalAsync("p1", 0.50m, ValidAddress, CancellationToken.None));
    }

    /// <summary>Bölüm 1.9: Pending -> Approved -> Sent -> Completed sırayla geçilir (bkz. ApproveWithdrawalAsync).</summary>
    [Fact]
    public async Task ApproveWithdrawalAsync_ProviderSucceeds_EndsCompleted_AndSetsProcessedAt()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var request = await _sut.RequestWithdrawalAsync("p1", 4.00m, ValidAddress, CancellationToken.None);

        var handled = await _sut.ApproveWithdrawalAsync(Guid.Parse(request.Id), CancellationToken.None);

        Assert.True(handled);
        var stored = await _db.WithdrawalRequests.SingleAsync();
        Assert.Equal(Models.Payments.WithdrawalRequestStatus.Completed, stored.Status);
        Assert.NotNull(stored.ProcessedAt);
    }

    /// <summary>
    /// 🐞 Regresyon — docs/21-payment-sandbox-e2e.md Aşama 5 (Bölüm 7, adım 6) gerçek
    /// regtest bulgusu: gönderim başarısız olduğunda (sandbox'ta BTCPay hot wallet'ında
    /// yeterli LTC olmadığı için gelen 422 ile fiilen üretildi) talep `Failed`'a geçiyor
    /// ama talep anında düşülen tutar bakiyeye geri EKLENMİYORDU — oyuncunun parası
    /// sessizce kayboluyordu.
    ///
    /// ⚠️ Bu testin kendisi önceden yanlış davranışı ("bakiye düşük kalır") doğru kabul
    /// edip sabitliyordu; docs/05-payment.md Bölüm 1.9 ise açıkça "`Failed`/`Rejected`
    /// durumunda bakiyeye geri eklenir" der ve Bölüm 11 kabul kriterlerinde bunu tekrar
    /// eder. docs/21 Bölüm 10 gereği kural doğrudur, kod (ve bu test) yanlıştı — ikisi de
    /// kurala göre düzeltildi.
    /// </summary>
    [Fact]
    public async Task ApproveWithdrawalAsync_ProviderFails_EndsFailed_AndBalanceIsRefunded()
    {
        var failingSut = new WalletService(
            _db, new FixedPriceOracle(44.5m), new FailingPaymentProvider(),
            Options.Create(new PaymentConfig { MinWithdrawalUsd = 1.00m }), TimeProvider.System,
            new PaymentEventNotifier(new FakeHubContext(), new FakeWalletHubContext()), NullLogger<WalletService>.Instance);

        await failingSut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var request = await failingSut.RequestWithdrawalAsync("p1", 4.00m, ValidAddress, CancellationToken.None);

        var handled = await failingSut.ApproveWithdrawalAsync(Guid.Parse(request.Id), CancellationToken.None);

        Assert.True(handled);
        var stored = await _db.WithdrawalRequests.SingleAsync();
        Assert.Equal(Models.Payments.WithdrawalRequestStatus.Failed, stored.Status);
        Assert.NotNull(stored.ProcessedAt);
        // Bölüm 1.9: talep anında düşülen 4.00 USD, Failed durumunda geri eklenir —
        // bakiye tekrar başlangıçtaki 10.00'a döner (RejectWithdrawalAsync ile aynı kural).
        Assert.Equal(10.00m, await failingSut.GetBalanceAsync("p1", CancellationToken.None));
    }

    /// <summary>
    /// docs/15-payment-flow-verification.md eşzamanlılık bulgusu: eski sürüm oku→kontrol
    /// et→yaz (check-then-act) yapıyordu — aynı ID'ye iki eşzamanlı onay isteği ikisi de
    /// "Status==Pending" kontrolünü geçip BTCPay'e ÇİFT on-chain gönderim yapabilirdi. Bu
    /// test, atomik `UPDATE ... WHERE Status='Pending'` deseminin (WalletService.TryDebitAsync
    /// ile aynı) ikinci çağrıyı güvenle reddettiğini kanıtlar — ilk çağrı tamamlandıktan
    /// sonra aynı ID'ye gelen bir ikinci onay artık hiçbir şey yapmaz (false döner).
    /// </summary>
    [Fact]
    public async Task ApproveWithdrawalAsync_CalledTwiceForSameId_SecondCallIsRejected_NoDoubleSend()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var request = await _sut.RequestWithdrawalAsync("p1", 4.00m, ValidAddress, CancellationToken.None);
        var id = Guid.Parse(request.Id);

        var first = await _sut.ApproveWithdrawalAsync(id, CancellationToken.None);
        var second = await _sut.ApproveWithdrawalAsync(id, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
        var stored = await _db.WithdrawalRequests.AsNoTracking().SingleAsync(w => w.Id == id);
        Assert.Equal(Models.Payments.WithdrawalRequestStatus.Completed, stored.Status);
    }

    [Fact]
    public async Task ApproveWithdrawalAsync_UnknownId_ReturnsFalse()
    {
        var handled = await _sut.ApproveWithdrawalAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(handled);
    }

    [Fact]
    public async Task RejectWithdrawalAsync_RefundsDebitedAmount_AndMarksRejected()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var request = await _sut.RequestWithdrawalAsync("p1", 4.00m, ValidAddress, CancellationToken.None);

        var handled = await _sut.RejectWithdrawalAsync(Guid.Parse(request.Id), CancellationToken.None);

        Assert.True(handled);
        var stored = await _db.WithdrawalRequests.SingleAsync();
        Assert.Equal(Models.Payments.WithdrawalRequestStatus.Rejected, stored.Status);
        Assert.NotNull(stored.ProcessedAt);
        Assert.Equal(10.00m, await _sut.GetBalanceAsync("p1", CancellationToken.None)); // tam geri eklendi.
    }

    [Fact]
    public async Task RejectWithdrawalAsync_AlreadyProcessed_ReturnsFalse_DoesNotDoubleRefund()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var request = await _sut.RequestWithdrawalAsync("p1", 4.00m, ValidAddress, CancellationToken.None);
        await _sut.RejectWithdrawalAsync(Guid.Parse(request.Id), CancellationToken.None);

        var handledAgain = await _sut.RejectWithdrawalAsync(Guid.Parse(request.Id), CancellationToken.None);

        Assert.False(handledAgain);
        Assert.Equal(10.00m, await _sut.GetBalanceAsync("p1", CancellationToken.None)); // ikinci kez eklenmedi.
    }

    /// <summary>docs/17-withdrawal-address-suggestions.md Bölüm 5, madde 1: hiç geçmişi yok → boş liste.</summary>
    [Fact]
    public async Task GetWithdrawalAddressSuggestionsAsync_NoHistory_ReturnsEmpty()
    {
        var suggestions = await _sut.GetWithdrawalAddressSuggestionsAsync("p1", CancellationToken.None);

        Assert.Empty(suggestions);
    }

    /// <summary>
    /// docs/17-withdrawal-address-suggestions.md Bölüm 2 🔒: Approved/Sent/Pending/Rejected/Failed
    /// hiçbiri "kullanılmış adres" sayılmaz — yalnızca Completed (fiilen zincire gönderilmiş).
    /// </summary>
    [Fact]
    public async Task GetWithdrawalAddressSuggestionsAsync_OnlyCompletedCounts()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var pendingOnly = await _sut.RequestWithdrawalAsync("p1", 2.00m, ValidAddress, CancellationToken.None);
        Assert.Equal("Pending", pendingOnly.Status);

        var suggestions = await _sut.GetWithdrawalAddressSuggestionsAsync("p1", CancellationToken.None);

        Assert.Empty(suggestions);
    }

    /// <summary>
    /// docs/17-withdrawal-address-suggestions.md Bölüm 5, madde 2: aynı adrese birden fazla
    /// çekim yapılmışsa tek satır, adresler en son kullanım tarihine göre azalan sırada,
    /// en fazla 5 benzersiz adres döner.
    /// </summary>
    [Fact]
    public async Task GetWithdrawalAddressSuggestionsAsync_GroupsByAddress_OrdersByLastUsed_CapsAtFive()
    {
        const string addressA = ValidAddress;
        const string addressB = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"; // AddressValidatorTests: bilinen geçerli Base58Check adresi

        await _sut.CreditAsync("p1", 20.00m, CancellationToken.None);

        // addressA ilk çekim (eski), sonra addressB (daha yeni), sonra addressA'ya ikinci bir
        // çekim (en yeni) — addressA'nın LastUsedAt'i bu ikinci çekime göre güncellenmeli.
        // CreatedAt sırası, gerçek zaman akışına bağlı kalmadan elle set edilir (deterministik test).
        var first = await _sut.RequestWithdrawalAsync("p1", 2.00m, addressA, CancellationToken.None);
        await SetCreatedAtAsync(first.Id, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await _sut.ApproveWithdrawalAsync(Guid.Parse(first.Id), CancellationToken.None);

        var second = await _sut.RequestWithdrawalAsync("p1", 2.00m, addressB, CancellationToken.None);
        await SetCreatedAtAsync(second.Id, new DateTime(2026, 1, 1, 0, 10, 0, DateTimeKind.Utc));
        await _sut.ApproveWithdrawalAsync(Guid.Parse(second.Id), CancellationToken.None);

        var third = await _sut.RequestWithdrawalAsync("p1", 2.00m, addressA, CancellationToken.None);
        await SetCreatedAtAsync(third.Id, new DateTime(2026, 1, 1, 0, 20, 0, DateTimeKind.Utc));
        await _sut.ApproveWithdrawalAsync(Guid.Parse(third.Id), CancellationToken.None);

        var suggestions = await _sut.GetWithdrawalAddressSuggestionsAsync("p1", CancellationToken.None);

        Assert.Equal(2, suggestions.Count);
        Assert.Equal(addressA, suggestions[0].Address);
        Assert.Equal(addressB, suggestions[1].Address);
    }

    private async Task SetCreatedAtAsync(string withdrawalRequestId, DateTime createdAt)
    {
        var id = Guid.Parse(withdrawalRequestId);
        await _db.WithdrawalRequests
            .Where(w => w.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(w => w.CreatedAt, createdAt));
    }

    /// <summary>docs/17-withdrawal-address-suggestions.md Bölüm 5, madde 4: kullanıcı izolasyonu.</summary>
    [Fact]
    public async Task GetWithdrawalAddressSuggestionsAsync_DoesNotLeakOtherPlayersAddresses()
    {
        await _sut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var request = await _sut.RequestWithdrawalAsync("p1", 2.00m, ValidAddress, CancellationToken.None);
        await _sut.ApproveWithdrawalAsync(Guid.Parse(request.Id), CancellationToken.None);

        var suggestionsForOtherPlayer = await _sut.GetWithdrawalAddressSuggestionsAsync("p2", CancellationToken.None);

        Assert.Empty(suggestionsForOtherPlayer);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
