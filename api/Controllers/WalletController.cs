using System.Security.Claims;
using api.Models.Payments.Dtos;
using api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// docs/05-payment.md Bölüm 1.9, docs/07-pages.md `/cuzdan`: bakiye görüntüleme,
/// bakiye yükleme (top-up) invoice'ı açma, para çekme talebi oluşturma.
///
/// docs/11-auth.md Bölüm 0.4: PlayerId istemciden asla alınmaz — yalnızca doğrulanmış
/// JWT'nin sub claim'inden okunur (bkz. CurrentPlayerId). Route'lardaki eski
/// {playerId} segmenti ve request body'lerindeki PlayerId alanı bu yüzden kaldırıldı.
/// </summary>
[Authorize]
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

    private string CurrentPlayerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<WalletDto>> GetBalance(CancellationToken cancellationToken)
    {
        return Ok(await _walletService.GetBalanceDtoAsync(CurrentPlayerId, cancellationToken));
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<List<PaymentInvoiceDto>>> GetInvoiceHistory(CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.GetInvoiceHistoryAsync(CurrentPlayerId, cancellationToken));
    }

    /// <summary>docs/08-page-content.md Bölüm 3.9: `/cuzdan`'daki "Bekleyen Transferler" kartı.</summary>
    [HttpGet("withdrawals")]
    public async Task<ActionResult<List<WithdrawalRequestDto>>> GetPendingWithdrawalsForPlayer(CancellationToken cancellationToken)
    {
        return Ok(await _walletService.ListForPlayerAsync(CurrentPlayerId, cancellationToken));
    }

    [HttpPost("topup")]
    public async Task<ActionResult<PaymentInvoiceDto>> TopUp([FromBody] CreateTopUpInvoiceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _paymentService.CreateTopUpInvoiceAsync(CurrentPlayerId, request.AmountUsd, cancellationToken);
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
                CurrentPlayerId, request.AmountUsd, request.DestinationLtcAddress, cancellationToken);
            return Ok(dto);
        }
        catch (PaymentValidationException ex)
        {
            return BadRequest(new PaymentErrorResponse { Code = ex.Code, Message = ex.Message });
        }
    }
}
