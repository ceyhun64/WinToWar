using api.Models.Payments;
using api.Services.Payments;

namespace api.Tests;

/// <summary>Bölüm 1.5: regex ön filtre + gerçek Base58Check/Bech32 checksum kontrolü.</summary>
public class AddressValidatorTests
{
    [Fact]
    public void ValidBech32Address_IsAccepted()
    {
        // BIP-173 resmi test vektörü.
        var ok = AddressValidator.TryValidate("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4", out var format);

        Assert.True(ok);
        Assert.Equal(PayoutAddressFormat.Bech32, format);
    }

    [Fact]
    public void Bech32Address_WithCorruptedChecksum_IsRejected()
    {
        // Aynı adresin son karakterleri bozulmuş hali (checksum artık tutmuyor).
        var ok = AddressValidator.TryValidate("bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kygt080", out _);

        Assert.False(ok);
    }

    [Fact]
    public void ValidBase58CheckAddress_IsAccepted()
    {
        // Bitcoin genesis blok adresi — herkesçe bilinen, checksum'ı doğrulanmış bir Base58Check adresi.
        var ok = AddressValidator.TryValidate("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", out var format);

        Assert.True(ok);
        Assert.Equal(PayoutAddressFormat.Base58Check, format);
    }

    [Fact]
    public void Base58CheckAddress_WithCorruptedChecksum_IsRejected()
    {
        var ok = AddressValidator.TryValidate("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNb", out _);

        Assert.False(ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-address")]
    [InlineData("0")]
    public void GarbageInput_IsRejected(string input)
    {
        Assert.False(AddressValidator.TryValidate(input, out _));
    }
}
