using System.Numerics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using api.Models.Payments;

namespace api.Services.Payments;

/// <summary>
/// Bölüm 1.5: PayoutAddress doğrulaması — regex ön filtre + gerçek Base58Check
/// (SHA256d checksum) / Bech32 (BIP-173 polymod checksum) kontrolü. Belirli bir
/// coin/network'e (mainnet/testnet) kilitlenmez; format genel olarak doğrulanır.
/// </summary>
public static partial class AddressValidator
{
    [GeneratedRegex("^[a-zA-HJ-NP-Z0-9]{14,74}$")]
    private static partial Regex Bech32Prefilter();

    [GeneratedRegex("^[1-9A-HJ-NP-Za-km-z]{25,35}$")]
    private static partial Regex Base58Prefilter();

    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Bech32Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";

    public static bool TryValidate(string address, out PayoutAddressFormat format)
    {
        format = default;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        if (address.Contains('1') && Bech32Prefilter().IsMatch(address) && IsValidBech32(address))
        {
            format = PayoutAddressFormat.Bech32;
            return true;
        }

        if (Base58Prefilter().IsMatch(address) && IsValidBase58Check(address))
        {
            format = PayoutAddressFormat.Base58Check;
            return true;
        }

        return false;
    }

    private static bool IsValidBase58Check(string address)
    {
        BigInteger value = 0;
        foreach (var c in address)
        {
            var digit = Base58Alphabet.IndexOf(c);
            if (digit < 0)
            {
                return false;
            }

            value = value * 58 + digit;
        }

        var bodyBytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

        var leadingZeroChars = 0;
        foreach (var c in address)
        {
            if (c != '1')
            {
                break;
            }

            leadingZeroChars++;
        }

        var full = new byte[leadingZeroChars + bodyBytes.Length];
        Array.Copy(bodyBytes, 0, full, leadingZeroChars, bodyBytes.Length);

        if (full.Length < 5)
        {
            return false;
        }

        var payload = full[..^4];
        var checksum = full[^4..];
        var hash1 = SHA256.HashData(payload);
        var hash2 = SHA256.HashData(hash1);

        return hash2.AsSpan(0, 4).SequenceEqual(checksum);
    }

    private static bool IsValidBech32(string address)
    {
        if (address != address.ToLowerInvariant() && address != address.ToUpperInvariant())
        {
            return false;
        }

        var normalized = address.ToLowerInvariant();
        var separatorIndex = normalized.LastIndexOf('1');
        if (separatorIndex < 1 || separatorIndex + 7 > normalized.Length)
        {
            return false;
        }

        var hrp = normalized[..separatorIndex];
        var dataPart = normalized[(separatorIndex + 1)..];

        var values = new int[dataPart.Length];
        for (var i = 0; i < dataPart.Length; i++)
        {
            var idx = Bech32Charset.IndexOf(dataPart[i]);
            if (idx < 0)
            {
                return false;
            }

            values[i] = idx;
        }

        return Polymod(HrpExpand(hrp).Concat(values).ToArray()) == 1;
    }

    private static IEnumerable<int> HrpExpand(string hrp)
    {
        foreach (var c in hrp)
        {
            yield return c >> 5;
        }

        yield return 0;

        foreach (var c in hrp)
        {
            yield return c & 31;
        }
    }

    private static int Polymod(int[] values)
    {
        int[] generator = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3];
        var chk = 1;
        foreach (var v in values)
        {
            var top = chk >> 25;
            chk = ((chk & 0x1ffffff) << 5) ^ v;
            for (var i = 0; i < 5; i++)
            {
                if (((top >> i) & 1) != 0)
                {
                    chk ^= generator[i];
                }
            }
        }

        return chk;
    }
}
