using api.Models.Payments.Dtos;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// Bölüm 3.1: giriş ücreti ödeme akışının REST giriş noktası. Ödeme modülü ana
/// oyun motorundan ayrı bir katman olduğundan bu controller MatchesController'a
/// dokunmadan eklenmiştir.
/// </summary>
[ApiController]
[Route("api/matches/{matchId}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// docs/05-payment.md Bölüm 1.9: yalnızca bakiye giriş ücretine yetmediğinde
    /// çağrılır — tutar (shortfall) her zaman sunucuda hesaplanır, client'tan
    /// gelmez (bkz. PaymentService.CreateMatchEntryInvoiceAsync).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PaymentInvoiceDto>> CreateInvoice(
        string matchId, [FromBody] CreatePaymentInvoiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _paymentService.CreateMatchEntryInvoiceAsync(
                matchId, request.PlayerId, request.PlayerName, request.PayoutAddress, cancellationToken);
            return Ok(dto);
        }
        catch (PaymentValidationException ex)
        {
            return BadRequest(new PaymentErrorResponse { Code = ex.Code, Message = ex.Message });
        }
        catch (PriceOracleUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new PaymentErrorResponse { Code = "PRICE_ORACLE_UNAVAILABLE", Message = ex.Message });
        }
    }

    [HttpGet("{invoiceId}")]
    public async Task<ActionResult<PaymentInvoiceDto>> GetInvoice(string matchId, string invoiceId, CancellationToken cancellationToken)
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
