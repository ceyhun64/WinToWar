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
        _sut = new WalletService(
            _db, new FixedPriceOracle(44.5m), new FakePaymentProvider(NullLogger<FakePaymentProvider>.Instance),
            Options.Create(config), TimeProvider.System, NullLogger<WalletService>.Instance);
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

    [Fact]
    public async Task ApproveWithdrawalAsync_ProviderFails_EndsFailed_AndBalanceStaysDebited()
    {
        var failingSut = new WalletService(
            _db, new FixedPriceOracle(44.5m), new FailingPaymentProvider(),
            Options.Create(new PaymentConfig { MinWithdrawalUsd = 1.00m }), TimeProvider.System, NullLogger<WalletService>.Instance);

        await failingSut.CreditAsync("p1", 10.00m, CancellationToken.None);
        var request = await failingSut.RequestWithdrawalAsync("p1", 4.00m, ValidAddress, CancellationToken.None);

        var handled = await failingSut.ApproveWithdrawalAsync(Guid.Parse(request.Id), CancellationToken.None);

        Assert.True(handled);
        var stored = await _db.WithdrawalRequests.SingleAsync();
        Assert.Equal(Models.Payments.WithdrawalRequestStatus.Failed, stored.Status);
        // Bölüm 1.9: Failed durumunda bakiye WalletService.RejectWithdrawalAsync'in aksine geri EKLENMEZ
        // (ApproveWithdrawalAsync yalnızca Rejected'de iade eder) — gönderim denenmiş ama başarısız olmuştur,
        // gerçek BTCPay entegrasyonunda bu durum admin'in elle incelemesini gerektirir.
        Assert.Equal(6.00m, await failingSut.GetBalanceAsync("p1", CancellationToken.None));
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

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
