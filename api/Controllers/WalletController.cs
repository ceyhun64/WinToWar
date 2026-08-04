using System.Globalization;
using api.Models.Payments.Dtos;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers;

/// <summary>
/// docs/05-payment.md Bölüm 1.9, docs/07-pages.md `/cuzdan`: bakiye görüntüleme,
/// bakiye yükleme (top-up) invoice'ı açma, para çekme talebi oluşturma.
/// </summary>
[ApiController]
[Route("api/wallet")]
public class WalletController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly PaymentService _paymentService;

    public WalletController(WalletService walletService, PaymentService paymentService)
    {
        _walletService = walletService;
        _paymentService = paymentService;
    }

    [HttpGet("{playerId}")]
    public async Task<ActionResult<WalletDto>> GetBalance(string playerId, CancellationToken cancellationToken)
    {
        var balance = await _walletService.GetBalanceAsync(playerId, cancellationToken);
        return Ok(new WalletDto { PlayerId = playerId, BalanceUsd = balance.ToString("0.00", CultureInfo.InvariantCulture) });
    }

    [HttpGet("{playerId}/invoices")]
    public async Task<ActionResult<List<PaymentInvoiceDto>>> GetInvoiceHistory(string playerId, CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.GetInvoiceHistoryAsync(playerId, cancellationToken));
    }

    [HttpPost("topup")]
    public async Task<ActionResult<PaymentInvoiceDto>> TopUp([FromBody] CreateTopUpInvoiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _paymentService.CreateTopUpInvoiceAsync(request.PlayerId, request.AmountUsd, request.PayoutAddress, cancellationToken);
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

    [HttpPost("withdraw")]
    public async Task<ActionResult<WithdrawalRequestDto>> RequestWithdrawal(
        [FromBody] RequestWithdrawalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _walletService.RequestWithdrawalAsync(
                request.PlayerId, request.AmountUsd, request.DestinationLtcAddress, cancellationToken);
            return Ok(dto);
        }
        catch (PaymentValidationException ex)
        {
            return BadRequest(new PaymentErrorResponse { Code = ex.Code, Message = ex.Message });
        }
    }
}
