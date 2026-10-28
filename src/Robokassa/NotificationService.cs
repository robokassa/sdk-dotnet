using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Robokassa.Models;

namespace Robokassa;

public sealed class VerifiedNotification
{
    public string OutSum { get; }
    public decimal Amount { get; }
    public string InvId { get; }
    public long InvoiceId { get; }
    public IReadOnlyDictionary<string, string> ShpFields { get; }
    public bool IsResultNotification { get; }

    internal VerifiedNotification(string sum, string id, Dictionary<string, string> shp, bool isResult)
    {
        OutSum = sum;
        Amount = Wire.Amount(sum);
        InvId = id;
        InvoiceId = long.Parse(id, CultureInfo.InvariantCulture);
        ShpFields = new ReadOnlyDictionary<string, string>(shp);
        IsResultNotification = isResult;
    }
}

public sealed class NotificationService
{
    private readonly RobokassaOptions options;
    internal NotificationService(RobokassaOptions options) { this.options = options; }

    public VerifiedNotification VerifyResultUrl(IEnumerable<KeyValuePair<string, string>> fields, decimal? expectedAmount = null) =>
        Verify(fields, options.NotificationPassword, true, expectedAmount);

    public VerifiedNotification VerifySuccessUrl(IEnumerable<KeyValuePair<string, string>> fields) =>
        Verify(fields, options.PaymentPassword, false, null);

    public static string ResultResponse(VerifiedNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (!notification.IsResultNotification) throw new ArgumentException("для подтверждения нужен ResultURL");
        return "OK" + notification.InvId;
    }

    private VerifiedNotification Verify(IEnumerable<KeyValuePair<string, string>> fields, string password, bool result, decimal? expectedAmount)
    {
        ArgumentNullException.ThrowIfNull(fields);
        const string operation = "notifications.verify";
        var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var shp = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in fields)
        {
            if (pair.Value is null || !all.TryAdd(pair.Key, pair.Value)) throw new RobokassaException(RobokassaErrorKind.InvalidSignature, operation);
            if (pair.Key.StartsWith("Shp_", StringComparison.OrdinalIgnoreCase)) shp.Add(pair.Key, pair.Value);
        }
        if (!all.TryGetValue("OutSum", out var sum) || !all.TryGetValue("InvId", out var id) ||
            !all.TryGetValue("SignatureValue", out var actual) || id.Length == 0 || id.Any(c => c < '0' || c > '9') ||
            !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
            throw new RobokassaException(RobokassaErrorKind.InvalidSignature, operation);
        var parts = new List<string> { sum, id, password };
        try { parts.AddRange(Wire.Shp(shp)); }
        catch (ArgumentException) { throw new RobokassaException(RobokassaErrorKind.InvalidSignature, operation); }
        var expected = RobokassaSignature.Hash(string.Join(":", parts), options.HashAlgorithm);
        if (!RobokassaSignature.EqualsHex(expected, actual)) throw new RobokassaException(RobokassaErrorKind.InvalidSignature, operation);
        VerifiedNotification notification;
        try { notification = new VerifiedNotification(sum, id, shp, result); }
        catch (ArgumentException) { throw new RobokassaException(RobokassaErrorKind.InvalidResponse, operation); }
        if (expectedAmount is { } amount && notification.Amount != amount)
            throw new RobokassaException(RobokassaErrorKind.InvalidResponse, operation);
        return notification;
    }

    public ResultUrl2Notification VerifyResultUrl2(string compactJws, X509Certificate2 trustedCertificate,
        string expectedAlgorithm = "RS256", TimeSpan? maxAge = null, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(compactJws);
        ArgumentNullException.ThrowIfNull(trustedCertificate);
        const string operation = "notifications.resultUrl2";
        try
        {
            if (compactJws.Length > options.MaxResponseBytes) throw new FormatException();
            var parts = compactJws.Split('.');
            if (parts.Length != 3) throw new FormatException();
            using var header = JsonDocument.Parse(RobokassaSignature.DecodeBase64Url(parts[0]));
            var root = header.RootElement;
            if (root.GetProperty("alg").GetString() != expectedAlgorithm || root.TryGetProperty("crit", out _) ||
                root.TryGetProperty("b64", out _)) throw new FormatException();
            var algorithm = expectedAlgorithm switch
            {
                "RS256" => HashAlgorithmName.SHA256,
                "RS384" => HashAlgorithmName.SHA384,
                "RS512" => HashAlgorithmName.SHA512,
                _ => throw new FormatException()
            };
            using var rsa = trustedCertificate.GetRSAPublicKey();
            if (rsa is null || !rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]),
                RobokassaSignature.DecodeBase64Url(parts[2]), algorithm, RSASignaturePadding.Pkcs1)) throw new FormatException();
            var payload = JsonSerializer.Deserialize<ResultUrl2Notification>(RobokassaSignature.DecodeBase64Url(parts[1]), Wire.Json);
            if (payload?.Data is null || payload.Header is null || payload.Data.Shop != options.MerchantLogin ||
                payload.Header.Type != "PaymentStateNotification" || string.IsNullOrWhiteSpace(payload.Data.InvId) ||
                string.IsNullOrWhiteSpace(payload.Data.State)) throw new FormatException();
            if (maxAge is { } age)
            {
                if (age <= TimeSpan.Zero || !long.TryParse(payload.Header.Timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
                    throw new FormatException();
                var elapsed = (now ?? DateTimeOffset.UtcNow) - DateTimeOffset.FromUnixTimeSeconds(timestamp);
                if (elapsed < TimeSpan.FromMinutes(-1) || elapsed > age) throw new FormatException();
            }
            return payload;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException or
            KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new RobokassaException(RobokassaErrorKind.InvalidSignature, operation);
        }
    }
}
