using api.Models.Payments.Dtos;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// docs/07-pages.md `/odeme/[invoiceId]`: bir invoice'ın MatchId'si olsun ya da
/// olmasın (top-up invoice'larında null — bkz. docs/05-payment.md Bölüm 1.9)
/// tek, matchId'den bağımsız bir sorgu ucu. PaymentsController'daki matchId-scoped
/// GET, maç akışına özgü bağlamlar için ayrıca korunur.
/// </summary>
[ApiController]
[Route("api/payments")]
public class InvoicesController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public InvoicesController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet("{invoiceId}")]
    public async Task<ActionResult<PaymentInvoiceDto>> GetInvoice(string invoiceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(invoiceId, out var id))
        {
            return BadRequest(new PaymentErrorResponse { Code = "INVALID_INVOICE_ID", Message = "Geçersiz invoice id." });
        }

        try
        {
            return Ok(await _paymentService.GetInvoiceAsync(id, cancellationToken));
        }
        catch (PaymentInvoiceNotFoundException)
        {
            return NotFound(new PaymentErrorResponse { Code = "INVOICE_NOT_FOUND", Message = "Invoice bulunamadı." });
        }
    }
}
