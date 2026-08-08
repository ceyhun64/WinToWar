using System.Globalization;
using System.Text.Json;
using api;
using api.Services.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace api.Controllers;

/// <summary>
/// 🛠️ Bölüm 0.3 ön koşulu: BTCPay regtest/testnet erişilemediği için
/// FakePaymentProvider kullanılıyor. Gerçek bir BTCPay olmadan invoice'ın
/// "ödendi" webhook'unu tetikleyecek hiçbir dış sistem yok — bu uç, gerçek
/// webhook işleme pipeline'ını (imza doğrulama, idempotency, monotonluk) uçtan
/// uca test edebilmek için BTCPay'in üreteceği webhook payload'ını simüle edip
/// AYNI PaymentService.HandleWebhookAsync yoluna gönderir. Yalnızca Development
/// ortamında ve yalnızca IPaymentProvider FakePaymentProvider ise çalışır;
/// mainnet/gerçek provider'a geçildiğinde bu uç otomatik olarak devre dışı kalır.
/// </summary>
[ApiController]
[Route("api/dev/payments")]
public class PaymentsDevController : ControllerBase
{
    private readonly PaymentService _paymentService;
    private readonly IPaymentProvider _paymentProvider;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly IWebHostEnvironment _environment;

    public PaymentsDevController(
        PaymentService paymentService,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        IWebHostEnvironment environment)
    {
        _paymentService = paymentService;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _environment = environment;
    }

    [HttpPost("{invoiceId}/simulate-paid")]
    public async Task<IActionResult> SimulatePaid(string invoiceId, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() || _paymentProvider is not FakePaymentProvider)
        {
            return NotFound();
        }

        if (!Guid.TryParse(invoiceId, out var id))
        {
            return BadRequest();
        }

        var details = await _paymentService.GetSimulationDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return NotFound();
        }

        var payload = JsonSerializer.Serialize(new
        {
            deliveryId = $"dev-{Guid.NewGuid():N}",
            type = BtcPayWebhookEventTypes.InvoiceSettled,
            timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            invoiceId = details.Value.BtcPayInvoiceId,
            confirmations = _config.RequiredConfirmations,
            paidAmountLtc = details.Value.AmountLtc.ToString("0.00000000", CultureInfo.InvariantCulture)
        });

        var signatureHeader = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);
        await _paymentService.HandleWebhookAsync(payload, signatureHeader, cancellationToken);

        return Ok();
    }
}
