using api.Models.Payments.Dtos;
using api.Services;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

public record AdminUserResponse(
    string PlayerId,
    string BalanceUsd,
    List<PaymentInvoiceDto> Invoices
);

/// <summary>
/// docs/07-pages.md `/admin/kullanicilar`: kullanıcı arama, bakiye görüntüleme.
/// 🛠️ Kapsam sınırlaması: hesap askıya alma henüz uygulanmadı — projede bir
/// kullanıcı/rol tablosu yok (bkz. AdminConfig.cs gerekçesi), "askıya alma"
/// anlamlı bir kalıcı kayıt gerektirir; bu, gerçek bir auth sistemiyle birlikte
/// ele alınacak ayrı bir görevdir (docs da bu kapsamı ❓ olarak işaretliyor).
/// </summary>
[ApiController]
[AdminAuth]
[Route("api/admin/users")]
public class AdminUsersController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly PaymentService _paymentService;

    public AdminUsersController(WalletService walletService, PaymentService paymentService)
    {
        _walletService = walletService;
        _paymentService = paymentService;
    }

    [HttpGet("{playerId}")]
    public async Task<ActionResult<AdminUserResponse>> Get(
        string playerId,
        CancellationToken cancellationToken
    )
    {
        var balance = await _walletService.GetBalanceAsync(playerId, cancellationToken);
        var invoices = await _paymentService.GetInvoiceHistoryAsync(playerId, cancellationToken);
        return Ok(
            new AdminUserResponse(
                playerId,
                balance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                invoices
            )
        );
    }
}
