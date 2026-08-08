using System.Security.Claims;
using api.Models.Payments.Dtos;
using api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// docs/07-pages.md `/odeme/[invoiceId]`: bir invoice'ın MatchId'si olsun ya da
/// olmasın (top-up invoice'larında null — bkz. docs/05-payment.md Bölüm 1.9)
/// tek, matchId'den bağımsız bir sorgu ucu. PaymentsController'daki matchId-scoped
/// GET, maç akışına özgü bağlamlar için ayrıca korunur.
/// </summary>
[Authorize]
[ApiController]
[Route("api/payments")]
public class InvoicesController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public InvoicesController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// 🔒 Yetki Matrisi (docs/07-pages.md): yalnızca invoice'ın sahibi görebilir.
    /// docs/11-auth.md Bölüm 0.4: sahiplik artık JWT'nin sub claim'inden okunan
    /// playerId ile invoice.PlayerId'nin eşleşmesiyle doğrulanır (client'tan gelen
    /// bir query parametresiyle değil). Eşleşmezse, invoice'ın varlığını bile
    /// sızdırmamak için 404 döner (403 değil).
    /// </summary>
    [HttpGet("{invoiceId}")]
    public async Task<ActionResult<PaymentInvoiceDto>> GetInvoice(string invoiceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(invoiceId, out var id))
        {
            return BadRequest(new PaymentErrorResponse { Code = "INVALID_INVOICE_ID", Message = "Geçersiz invoice id." });
        }

        var playerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var dto = await _paymentService.GetInvoiceAsync(id, cancellationToken);
            if (dto.PlayerId != playerId)
            {
                return NotFound(new PaymentErrorResponse { Code = "INVOICE_NOT_FOUND", Message = "Invoice bulunamadı." });
            }

            return Ok(dto);
        }
        catch (PaymentInvoiceNotFoundException)
        {
            return NotFound(new PaymentErrorResponse { Code = "INVOICE_NOT_FOUND", Message = "Invoice bulunamadı." });
        }
    }
}
