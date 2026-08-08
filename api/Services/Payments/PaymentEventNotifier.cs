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

    public PaymentEventNotifier(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyPaymentConfirmedAsync(string matchId, PaymentConfirmedEvent payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(matchId).SendAsync("PaymentConfirmed", payload, cancellationToken);

    public Task NotifyPayoutCompletedAsync(string matchId, PayoutCompletedEvent payload, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(matchId).SendAsync("PayoutCompleted", payload, cancellationToken);
}
