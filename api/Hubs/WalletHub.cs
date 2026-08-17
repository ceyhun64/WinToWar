using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace api.Hubs;

/// <summary>
/// docs/16-wallet-balance-sync.md Bölüm 1: bakiye değişimlerini anlık olarak
/// istemciye taşımak için GameHub'dan ayrı, ince bir hub — GameHub maç/bölge
/// state'i taşır, bakiye bilgisini oraya karıştırmak SRP ihlali olurdu (bkz.
/// docs/01-workflow-rules.md Bölüm 0.13 modüller arası izolasyon). Burada hiçbir
/// iş mantığı yoktur; yalnızca bağlanan kullanıcıyı kendi `wallet:{userId}`
/// grubuna ekler, yayın PaymentEventNotifier üzerinden yapılır (bkz. WalletService).
///
/// [Authorize] + Context.UserIdentifier (JWT sub claim) deseni GameHub ile
/// birebir aynıdır (bkz. docs/11-auth.md Bölüm 0.4/3.5) — kullanıcı kendi
/// grubundan başka bir grup adı asla client'tan alınmaz, bu yüzden başka bir
/// kullanıcının bakiyesi sızamaz.
/// </summary>
[Authorize]
public class WalletHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var playerId = Context.UserIdentifier!;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"wallet:{playerId}");
        await base.OnConnectedAsync();
    }
}
