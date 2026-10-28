using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Robokassa;

public static class RobokassaSignature
{
    public static string Hash(string value, RobokassaHashAlgorithm algorithm)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var digest = algorithm switch
        {
            RobokassaHashAlgorithm.Md5 => MD5.HashData(bytes),
            RobokassaHashAlgorithm.Sha1 => SHA1.HashData(bytes),
            RobokassaHashAlgorithm.Sha256 => SHA256.HashData(bytes),
            RobokassaHashAlgorithm.Sha384 => SHA384.HashData(bytes),
            RobokassaHashAlgorithm.Sha512 => SHA512.HashData(bytes),
            RobokassaHashAlgorithm.Ripemd160 => Ripemd(bytes),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public static bool EqualsHex(string expected, string? actual)
    {
        if (actual is null || actual.Length != expected.Length || expected.Length % 2 != 0) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual)); }
        catch (FormatException) { return false; }
    }

    internal static string Jwt(object payload, string key, RobokassaHashAlgorithm algorithm)
    {
        var alg = algorithm switch
        {
            RobokassaHashAlgorithm.Md5 => "MD5",
            RobokassaHashAlgorithm.Ripemd160 => "RIPEMD160",
            RobokassaHashAlgorithm.Sha1 => "HS1",
            RobokassaHashAlgorithm.Sha256 => "HS256",
            RobokassaHashAlgorithm.Sha384 => "HS384",
            RobokassaHashAlgorithm.Sha512 => "HS512",
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
        var input = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg })) + "." +
            Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, Wire.Json));
        var bytes = Encoding.UTF8.GetBytes(input);
        var secret = Encoding.UTF8.GetBytes(key);
        byte[] result;
        if (algorithm == RobokassaHashAlgorithm.Ripemd160)
        {
            var hmac = new HMac(new RipeMD160Digest());
            hmac.Init(new KeyParameter(secret));
            hmac.BlockUpdate(bytes, 0, bytes.Length);
            result = new byte[hmac.GetMacSize()];
            hmac.DoFinal(result, 0);
        }
        else
        {
            using HMAC hmac = algorithm switch
            {
                RobokassaHashAlgorithm.Md5 => new HMACMD5(secret),
                RobokassaHashAlgorithm.Sha1 => new HMACSHA1(secret),
                RobokassaHashAlgorithm.Sha256 => new HMACSHA256(secret),
                RobokassaHashAlgorithm.Sha384 => new HMACSHA384(secret),
                RobokassaHashAlgorithm.Sha512 => new HMACSHA512(secret),
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
            };
            result = hmac.ComputeHash(bytes);
        }
        return input + "." + Base64Url(result);
    }

    private static byte[] Ripemd(byte[] bytes)
    {
        var hash = new RipeMD160Digest();
        hash.BlockUpdate(bytes, 0, bytes.Length);
        var result = new byte[hash.GetDigestSize()];
        hash.DoFinal(result, 0);
        return result;
    }

    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    internal static byte[] DecodeBase64Url(string value)
    {
        if (value.Length == 0 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new FormatException();
        return Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    }
}
