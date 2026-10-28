using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Robokassa.Tests;

internal sealed class StubHandler : HttpMessageHandler
{
    internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(Json("{\"isSuccess\":true}"));
    internal int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Respond(request, cancellationToken);
    }
    internal static HttpResponseMessage Json(string value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
}

internal sealed class TestClient : IDisposable
{
    internal StubHandler Handler { get; } = new();
    private readonly HttpClient http;
    internal RobokassaClient Client { get; }
    internal TestClient(bool test = false, RobokassaHashAlgorithm algorithm = RobokassaHashAlgorithm.Md5,
        TimeSpan? timeout = null, int maxBytes = 4 * 1024 * 1024)
    {
        http = new HttpClient(Handler);
        Client = new RobokassaClient(new RobokassaOptions
        {
            MerchantLogin = "demo", Password1 = "test", Password2 = "result", Password3 = "refund",
            TestPassword1 = "test1", TestPassword2 = "test2", IsTest = test, HashAlgorithm = algorithm,
            Timeout = timeout ?? TimeSpan.FromSeconds(15), MaxResponseBytes = maxBytes,
            Endpoints = new RobokassaEndpoints { Services = new("https://services.example/"), Merchant = new("https://merchant.example/") }
        }, http);
    }
    internal const string InvoiceResponse = """{"isSuccess":true,"id":"01234567-89ab-4cde-8f01-23456789abcd","invId":42,"url":"https://auth.robokassa.ru/merchant/Invoice/test"}""";
    public void Dispose() { Client.Dispose(); http.Dispose(); }
}

internal static class Inspect
{
    internal static byte[] Decode(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/') + new string('=', (4 - s.Length % 4) % 4));
    internal static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    internal static string B64(string s) => B64(Encoding.UTF8.GetBytes(s));
    internal static async Task<JsonElement> Jwt(HttpRequestMessage r, string key = "demo:test", string expectedAlg = "MD5")
    {
        Assert.Equal("application/json", r.Content!.Headers.ContentType!.MediaType);
        var raw = await r.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.Equal(JsonValueKind.String, json.RootElement.ValueKind);
        var parts = json.RootElement.GetString()!.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, p => Assert.DoesNotContain("=", p));
        using var header = JsonDocument.Parse(Decode(parts[0]));
        Assert.Equal(expectedAlg, header.RootElement.GetProperty("alg").GetString());
        var keyBytes = Encoding.UTF8.GetBytes(key);
        using HMAC hmac = expectedAlg switch
        {
            "MD5" => new HMACMD5(keyBytes), "HS1" => new HMACSHA1(keyBytes), "HS256" => new HMACSHA256(keyBytes),
            "HS384" => new HMACSHA384(keyBytes), "HS512" => new HMACSHA512(keyBytes), _ => throw new ArgumentException()
        };
        Assert.Equal(hmac.ComputeHash(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1])), Decode(parts[2]));
        return JsonDocument.Parse(Decode(parts[1])).RootElement.Clone();
    }
    internal static Dictionary<string, string> Form(string data) => data.Split('&').Select(p => p.Split('=', 2)).ToDictionary(
        p => Uri.UnescapeDataString(p[0].Replace('+', ' ')), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
}
