using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Siphon.Media.Delivery;

public enum TokenVerdict { Valid, Invalid, Expired }

public sealed class FileTokenService(IOptions<SiphonOptions> options)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.FileTokenSecret);

    public string Issue(string jobId, DateTimeOffset expiresAt)
    {
        var payload = $"{jobId}:{expiresAt.ToUnixTimeSeconds()}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var mac = HMACSHA256.HashData(_key, payloadBytes);
        return $"{Base64Url(payloadBytes)}.{Base64Url(mac)}";
    }

    public TokenVerdict Validate(string jobId, string token)
    {
        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1) return TokenVerdict.Invalid;
        byte[] payloadBytes, mac;
        try
        {
            payloadBytes = FromBase64Url(token[..dot]);
            mac = FromBase64Url(token[(dot + 1)..]);
        }
        catch (FormatException)
        {
            return TokenVerdict.Invalid;
        }

        var expected = HMACSHA256.HashData(_key, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(expected, mac)) return TokenVerdict.Invalid;

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var sep = payload.LastIndexOf(':');
        if (sep <= 0) return TokenVerdict.Invalid;
        if (payload[..sep] != jobId) return TokenVerdict.Invalid;
        if (!long.TryParse(payload[(sep + 1)..], out var expiresUnix)) return TokenVerdict.Invalid;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix ? TokenVerdict.Expired : TokenVerdict.Valid;
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
