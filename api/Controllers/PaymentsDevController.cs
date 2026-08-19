using System.Security.Claims;
using System.Text.Json;
using api;
using api.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace api.Controllers;

/// <summary>
/// 🛠️ `Payment:Mode=Fake` modda gerçek bir BTCPay olmadığından invoice'ın "ödendi"
/// webhook'unu tetikleyecek hiçbir dış sistem yoktur — bu uç, BTCPay'in üreteceği webhook
/// payload'ını simüle edip AYNI `PaymentService.HandleWebhookAsync` yoluna gönderir (imza
/// doğrulama, idempotency, monotonluk dahil gerçek pipeline çalışır).
///
/// 🔒 **Erişim koşulu = "para sahte mi", "ortam dev mi" DEĞİL.** Önceki hâlde uç
/// `IsDevelopment()` **ve** `FakePaymentProvider` koşullarını birlikte arıyordu; kullanıcı
/// talebi üzerine (canlıda da simülasyon butonu istendi) ortam koşulu kaldırıldı, **sahte
/// sağlayıcı koşulu korundu**. Doğru güvenlik sınırı budur: `Payment:Mode` `Sandbox` veya
/// `Live` yapıldığı anda `IPaymentProvider` artık `FakePaymentProvider` olmaz ve bu uç
/// kendiliğinden 404'e döner — yani gerçek para taşıyan bir kurulumda bakiyeyi bedavaya
/// şişirmek mümkün değildir. Fail-closed: unutulacak bir bayrak yoktur, koşul sistemin
/// kendi durumundan okunur.
///
/// 🔒 **Kimlik/sahiplik.** Uç artık `[Authorize]`'dır ve yalnızca **çağıranın kendi**
/// invoice'ını ödenmiş sayabilir. Bu iki koruma eskiden yoktu; Development'ta zararsızdı
/// ama uç canlıya açıldığında kimliksiz bir istekle başkasının invoice'ını kapatmak
/// mümkün olurdu.
/// </summary>
[Authorize]
[ApiController]
[Route("api/dev/payments")]
public class PaymentsDevController : ControllerBase
{
    private readonly PaymentService _paymentService;
    private readonly IPaymentProvider _paymentProvider;
    private readonly PaymentConfig _config;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentsDevController> _logger;

    public PaymentsDevController(
        PaymentService paymentService,
        IPaymentProvider paymentProvider,
        IOptions<PaymentConfig> config,
        TimeProvider timeProvider,
        ILogger<PaymentsDevController> logger)
    {
        _paymentService = paymentService;
        _paymentProvider = paymentProvider;
        _config = config.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private string CurrentPlayerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Simülasyonun bu kurulumda mümkün olup olmadığı — sağlayıcı sahte mi.</summary>
    private bool SimulationAvailable => _paymentProvider is FakePaymentProvider;

    /// <summary>
    /// Frontend'in "Ödemeyi Simüle Et" butonunu gösterip göstermeyeceğine karar vermesi için.
    ///
    /// 🛠️ Neden ayrı bir uç: buton eskiden `process.env.NODE_ENV !== "production"` ile
    /// gizleniyordu, yani karar **derleme zamanında** ve sunucunun gerçek durumundan bağımsız
    /// veriliyordu. Artık tek doğruluk kaynağı sunucudur — `Payment:Mode` değiştiğinde
    /// frontend'i yeniden derlemeye gerek kalmadan buton kendiliğinden görünür/kaybolur ve
    /// "buton var ama uç 404" tutarsızlığı yapısal olarak imkânsız hale gelir.
    /// </summary>
    [HttpGet("availability")]
    public ActionResult<PaymentSimulationAvailabilityDto> GetAvailability()
    {
        return Ok(new PaymentSimulationAvailabilityDto { Available = SimulationAvailable });
    }

    [HttpPost("{invoiceId}/simulate-paid")]
    public async Task<IActionResult> SimulatePaid(string invoiceId, CancellationToken cancellationToken)
    {
        if (!SimulationAvailable)
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

        // Sahiplik doğrulaması: başkasının invoice'ının varlığını sızdırmamak için 403
        // değil 404 döner (bkz. InvoicesController'daki aynı desen).
        if (details.Value.PlayerId != CurrentPlayerId)
        {
            _logger.LogWarning(
                "Ödeme simülasyonu reddedildi: {PlayerId} kendisine ait olmayan {InvoiceId} invoice'ını kapatmayı denedi.",
                CurrentPlayerId, id);
            return NotFound();
        }

        var payload = JsonSerializer.Serialize(new
        {
            deliveryId = $"dev-{Guid.NewGuid():N}",
            type = BtcPayWebhookEventTypes.InvoiceSettled,
            timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            invoiceId = details.Value.BtcPayInvoiceId
        });

        var signatureHeader = WebhookSignatureValidator.ComputeSignatureHeader(payload, _config.WebhookSecret);
        await _paymentService.HandleWebhookAsync(payload, signatureHeader, cancellationToken);

        _logger.LogInformation(
            "Ödeme simüle edildi (sahte sağlayıcı): invoice={InvoiceId}, oyuncu={PlayerId}", id, CurrentPlayerId);

        return Ok();
    }
}

public class PaymentSimulationAvailabilityDto
{
    public bool Available { get; init; }
}
