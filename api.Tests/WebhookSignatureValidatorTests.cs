using api.Services.Payments;

namespace api.Tests;

/// <summary>Bölüm 8.1: webhook imza doğrulaması.</summary>
public class WebhookSignatureValidatorTests
{
    private const string Secret = "test-secret";

    [Fact]
    public void ValidSignature_IsAccepted()
    {
        var payload = "{\"deliveryId\":\"evt-1\"}";
        var header = WebhookSignatureValidator.ComputeSignatureHeader(payload, Secret);

        Assert.True(WebhookSignatureValidator.IsValid(payload, header, Secret));
    }

    [Fact]
    public void TamperedPayload_IsRejected()
    {
        var payload = "{\"deliveryId\":\"evt-1\"}";
        var header = WebhookSignatureValidator.ComputeSignatureHeader(payload, Secret);

        Assert.False(WebhookSignatureValidator.IsValid("{\"deliveryId\":\"evt-2\"}", header, Secret));
    }

    [Fact]
    public void WrongSecret_IsRejected()
    {
        var payload = "{\"deliveryId\":\"evt-1\"}";
        var header = WebhookSignatureValidator.ComputeSignatureHeader(payload, Secret);

        Assert.False(WebhookSignatureValidator.IsValid(payload, header, "different-secret"));
    }

    [Fact]
    public void MissingPrefix_IsRejected()
    {
        var payload = "{\"deliveryId\":\"evt-1\"}";
        var hexOnly = WebhookSignatureValidator.ComputeSignatureHeader(payload, Secret).Replace("sha256=", "");

        Assert.False(WebhookSignatureValidator.IsValid(payload, hexOnly, Secret));
    }

    [Fact]
    public void NullOrEmptyHeader_IsRejected()
    {
        Assert.False(WebhookSignatureValidator.IsValid("payload", null, Secret));
        Assert.False(WebhookSignatureValidator.IsValid("payload", "", Secret));
    }
}
