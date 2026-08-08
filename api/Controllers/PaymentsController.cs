using System.Security.Claims;
using api.Models.Payments.Dtos;
using api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// Bölüm 3.1: giriş ücreti ödeme akışının REST giriş noktası. Ödeme modülü ana
/// oyun motorundan ayrı bir katman olduğundan bu controller MatchesController'a
/// dokunmadan eklenmiştir.
///
/// docs/11-auth.md Bölüm 0.4: PlayerId istemciden asla alınmaz, JWT'nin sub
/// claim'inden okunur (bkz. CurrentPlayerId).
/// </summary>
[Authorize]
[ApiController]
[Route("api/matches/{matchId}/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    private string CurrentPlayerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

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
                matchId, CurrentPlayerId, request.PlayerName, cancellationToken);
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

    /// <summary>🔒 Yetki Matrisi: bkz. InvoicesController.GetInvoice'daki aynı sahiplik-doğrulama gerekçesi.</summary>
    [HttpGet("{invoiceId}")]
    public async Task<ActionResult<PaymentInvoiceDto>> GetInvoice(string matchId, string invoiceId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(invoiceId, out var id))
        {
            return BadRequest(new PaymentErrorResponse { Code = "INVALID_INVOICE_ID", Message = "Geçersiz invoice id." });
        }

        try
        {
            var dto = await _paymentService.GetInvoiceAsync(id, cancellationToken);
            if (dto.PlayerId != CurrentPlayerId)
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
