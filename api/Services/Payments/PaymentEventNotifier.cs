using api.Hubs;
using api.Models.Payments.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 10 (SignalR event entegrasyonu). Ödeme modülü ayrı bir katman olduğundan
/// GameHub'a dokunmaz; mevcut oyun state broadcast'inin kullandığı aynı match-id
/// grubuna (bkz. GameHub.BroadcastState) ek olay isimleriyle mesaj gönderir.
/// </summary>
public class PaymentEventNotifier
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly IHubContext<WalletHub> _walletHubContext;

    public PaymentEventNotifier(IHubContext<GameHub> hubContext, IHubContext<WalletHub> walletHubContext)
    {
        _hubContext = hubContext;
        _walletHubContext = walletHubContext;
    }

    public Task NotifyPaymentConfirmedAsync(string matchId, PaymentConfirmedEvent payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(matchId).SendAsync("PaymentConfirmed", payload, cancellationToken);

    public Task NotifyPayoutCompletedAsync(string matchId, PayoutCompletedEvent payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(matchId).SendAsync("PayoutCompleted", payload, cancellationToken);

    /// <summary>
    /// docs/16-wallet-balance-sync.md Bölüm 1: WalletHub'daki `wallet:{userId}`
    /// grubuna mutlak bakiyeyi yayınlar (delta değil) — aynı mesaj iki kez gelse
    /// bile state aynı değere set edilir, ayrı bir idempotency mekanizması gerekmez.
    /// </summary>
    public Task NotifyWalletBalanceChangedAsync(string playerId, WalletDto balance, CancellationToken cancellationToken) =>
        _walletHubContext.Clients.Group($"wallet:{playerId}").SendAsync("WalletBalanceUpdated", balance, cancellationToken);
}
