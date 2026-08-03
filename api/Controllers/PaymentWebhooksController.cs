using api;
using api.Models.Payments.Dtos;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace api.Controllers;

/// <summary>Bölüm 3.1: BTCPay'den gelen webhook'ların alındığı uç.</summary>
[ApiController]
[Route("api/webhooks/btcpay")]
public class PaymentWebhooksController : ControllerBase
{
    private readonly PaymentService _paymentService;
    private readonly PaymentConfig _config;
    private readonly ILogger<PaymentWebhooksController> _logger;

    public PaymentWebhooksController(PaymentService paymentService, IOptions<PaymentConfig> config, ILogger<PaymentWebhooksController> logger)
    {
        _paymentService = paymentService;
        _config = config.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        Request.Headers.TryGetValue(_config.WebhookSignatureHeader, out var signatureValues);

        try
        {
            await _paymentService.HandleWebhookAsync(body, signatureValues.FirstOrDefault(), cancellationToken);
            return Ok();
        }
        catch (PaymentValidationException ex)
        {
            _logger.LogWarning("Webhook reddedildi: {Code} {Message}", ex.Code, ex.Message);
            return BadRequest(new PaymentErrorResponse { Code = ex.Code, Message = ex.Message });
        }
    }
}
