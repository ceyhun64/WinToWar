using api.Models.Payments.Dtos;
using api.Services;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>docs/07-pages.md `/admin/odemeler`: bekleyen çekim talepleri, başarısız invoice'lar, manuel onay/red.</summary>
[ApiController]
[AdminAuth]
[Route("api/admin/payments")]
public class AdminPaymentsController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly PaymentService _paymentService;

    public AdminPaymentsController(WalletService walletService, PaymentService paymentService)
    {
        _walletService = walletService;
        _paymentService = paymentService;
    }

    [HttpGet("withdrawals")]
    public async Task<ActionResult<List<WithdrawalRequestDto>>> GetPendingWithdrawals(
        CancellationToken cancellationToken
    )
    {
        return Ok(await _walletService.ListPendingWithdrawalsAsync(cancellationToken));
    }

    [HttpPost("withdrawals/{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        return await _walletService.ApproveWithdrawalAsync(id, cancellationToken)
            ? Ok()
            : NotFound();
    }

    [HttpPost("withdrawals/{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        return await _walletService.RejectWithdrawalAsync(id, cancellationToken)
            ? Ok()
            : NotFound();
    }

    [HttpGet("invoices/failed")]
    public async Task<ActionResult<List<PaymentInvoiceDto>>> GetFailedInvoices(
        CancellationToken cancellationToken
    )
    {
        return Ok(await _paymentService.GetFailedInvoicesAsync(cancellationToken));
    }

    /// <summary>docs/05-payment.md Bölüm 10.1: teknik arıza kaynaklı, admin-onaylı manuel iade.</summary>
    [HttpPost("invoices/{invoiceId}/refund")]
    public async Task<IActionResult> RefundInvoice(
        Guid invoiceId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _paymentService.SubmitManualRefundAsync(invoiceId, cancellationToken);
            return Ok();
        }
        catch (PaymentInvoiceNotFoundException)
        {
            return NotFound(
                new PaymentErrorResponse
                {
                    Code = "INVOICE_NOT_FOUND",
                    Message = "Invoice bulunamadı.",
                }
            );
        }
        catch (PaymentValidationException ex)
        {
            return BadRequest(new PaymentErrorResponse { Code = ex.Code, Message = ex.Message });
        }
    }
}
