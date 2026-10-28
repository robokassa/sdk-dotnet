using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Robokassa.Models;
using Xunit;

namespace Robokassa.Tests;

public sealed class SignatureNotificationTests
{
    [Theory]
    [InlineData(RobokassaHashAlgorithm.Md5, "c76b1b2ce919d2475134b5de5b379618")]
    [InlineData(RobokassaHashAlgorithm.Ripemd160, "d2095bbe06043637f8e5815404686ce217d803b9")]
    [InlineData(RobokassaHashAlgorithm.Sha1, "5782845c756e9e852aea191d53b3b8e987adf61f")]
    [InlineData(RobokassaHashAlgorithm.Sha256, "c71475e78058610a889d314755fc7c9038fda650e70f4b0bfa8fb0f08a6e797a")]
    [InlineData(RobokassaHashAlgorithm.Sha384, "1fec598c208d8ae43d59027bf847f5fcfb964229b4b7197f6d56ab8e9026c3f22e89250538a908c7c94cf6b47447004c")]
    [InlineData(RobokassaHashAlgorithm.Sha512, "710bb513633aab791626479cd8400fb47bb2068a3af47a10f207c54988e670ef1252471de5eb291486a439ad8ea69c6f5b29047234e328f56c13012b796a5c33")]
    public void HashMatchesKnownSignatures(RobokassaHashAlgorithm algorithm, string expected)
    {
        Assert.Equal(expected, RobokassaSignature.Hash("demo:100.00:42:test", algorithm));
        using var t = new TestClient(algorithm: algorithm);
        var fields = t.Client.ClassicPayments.BuildPaymentForm(new() { OutSum = "100.00", InvId = 42 }).Fields.ToDictionary();
        Assert.Equal(expected, fields["SignatureValue"]);
    }

    [Fact]
    public void PaymentWithReceiptHoldAndCustomFieldsHasExpectedSignature()
    {
        using var t = new TestClient();
        var request = new PaymentRequest
        {
            OutSum = "100.00", InvId = 42, StepByStep = true, ResultUrl2 = "https://example.com/result?a=1&b=2",
            Receipt = new() { Items = [new() { Name = "тест", Quantity = 1, Sum = 100, Tax = "none" }] },
            ShpFields = new Dictionary<string, string> { ["Shp_b"] = "2", ["Shp_a"] = "заказ" }, PaymentMethods = ["BankCard", "SBP"]
        };
        var form = t.Client.ClassicPayments.BuildPaymentForm(request);
        Assert.Equal("d617f2340ff61089821c363f803260ca", form.Fields.Single(p => p.Key == "SignatureValue").Value);
        Assert.Equal(2, form.Fields.Count(p => p.Key == "PaymentMethods"));
        var url = t.Client.ClassicPayments.BuildPaymentUrl(request).AbsoluteUri;
        Assert.Contains("Receipt=%257B%2522items", url);
        Assert.Contains("ResultUrl2=https%253A%252F%252F", url);
        Assert.Equal(0, t.Handler.Calls);
    }

    [Fact]
    public void ExplicitFalseHoldingDoesNotEnterSignature()
    {
        using var t = new TestClient();
        var f = t.Client.ClassicPayments.BuildPaymentForm(new() { OutSum = "100.00", InvId = 42, StepByStep = false }).Fields.ToDictionary();
        Assert.Equal("false", f["StepByStep"]);
        Assert.Equal("c76b1b2ce919d2475134b5de5b379618", f["SignatureValue"]);
    }

    [Fact]
    public void SavedCardHasExpectedSignatureAndUsesDedicatedEndpoint()
    {
        using var t = new TestClient();
        var form = t.Client.ClassicPayments.BuildSavedCardForm(new() { OutSum = "100.00", InvId = 42, Token = "op-key" });
        Assert.Equal("/Merchant/Payment/CoFPayment", form.Action.AbsolutePath);
        Assert.Equal("45301fcd6a26528ed4d51252955560dc", form.Fields.ToDictionary()["SignatureValue"]);
    }

    [Fact]
    public void SplitSignsExactlyTheSerializedInvoiceBeforeEncoding()
    {
        using var t = new TestClient();
        var invoice = new SplitPaymentInvoice
        { OutAmount = 100, Merchant = new() { Id = "demo" }, SplitMerchants = [new() { Id = "partner", Amount = 100 }] };
        var form = t.Client.ClassicPayments.BuildSplitForm(invoice);
        var fields = form.Fields.ToDictionary();
        var raw = Uri.UnescapeDataString(fields["invoice"]);
        using var payload = JsonDocument.Parse(raw);
        Assert.Equal(100, payload.RootElement.GetProperty("outAmount").GetDecimal());
        var independent = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw + "test"))).ToLowerInvariant();
        Assert.Equal(independent, fields["signature"]);
        Assert.Equal("/Merchant/Payment/CreateV2", form.Action.AbsolutePath);
    }

    [Fact]
    public void ResultPreservesRawSumAndSortsShp()
    {
        using var client = new RobokassaClient(new() { MerchantLogin = "demo", Password2 = "test", IsTest = false });
        var fields = Fields();
        var result = client.Notifications.VerifyResultUrl(fields, 100m);
        Assert.Equal("100.000000", result.OutSum);
        Assert.Equal(100m, result.Amount);
        Assert.Equal("OK42", NotificationService.ResultResponse(result));
        Assert.Throws<RobokassaException>(() => client.Notifications.VerifyResultUrl(fields, 101m));
    }

    [Theory]
    [InlineData("OutSum", "101.000000")]
    [InlineData("InvId", "43")]
    [InlineData("SignatureValue", "gggg")]
    [InlineData("Shp_a", "подмена")]
    public void RejectsTamperedCallback(string key, string value)
    {
        using var client = new RobokassaClient(new() { MerchantLogin = "demo", Password2 = "test", IsTest = false });
        var fields = Fields();
        fields[key] = value;
        Assert.Throws<RobokassaException>(() => client.Notifications.VerifyResultUrl(fields));
    }

    [Fact]
    public void DuplicateCaseInsensitiveFieldsAreRejected()
    {
        using var t = new TestClient();
        var fields = Fields().ToList();
        fields.Add(new("outsum", "1"));
        Assert.Throws<RobokassaException>(() => t.Client.Notifications.VerifyResultUrl(fields));
    }

    [Fact]
    public void SuccessCannotBeUsedToAcknowledgeResult()
    {
        using var client = new RobokassaClient(new() { MerchantLogin = "demo", Password1 = "test", IsTest = false });
        var notification = client.Notifications.VerifySuccessUrl(Fields());
        Assert.False(notification.IsResultNotification);
        Assert.Throws<ArgumentException>(() => NotificationService.ResultResponse(notification));
    }

    [Fact]
    public void TestCallbacksUseTestPassword2()
    {
        using var t = new TestClient(test: true);
        var fields = new Dictionary<string, string>
        { ["OutSum"] = "100.00", ["InvId"] = "42", ["SignatureValue"] = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("100.00:42:test2"))) };
        Assert.Equal(100m, t.Client.Notifications.VerifyResultUrl(fields).Amount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1,00")]
    [InlineData("1e2")]
    [InlineData(" 1")]
    [InlineData("NaN")]
    public void InvalidAmountsFailBeforeSigning(string amount)
    {
        using var t = new TestClient();
        Assert.Throws<ArgumentException>(() => t.Client.ClassicPayments.BuildPaymentUrl(new() { OutSum = amount }));
    }

    [Fact]
    public void JwsRequiresTrustedCertificateCorrectMerchantAndSignature()
    {
        using var rsa = RSA.Create(2048);
        var csr = new CertificateRequest("CN=fixture", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = csr.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var t = new TestClient();
        var jws = MakeJws(rsa, "demo");
        var result = t.Client.Notifications.VerifyResultUrl2(jws, cert, maxAge: TimeSpan.FromMinutes(5));
        Assert.Equal("HOLD", result.Data.State);
        Assert.Throws<RobokassaException>(() => t.Client.Notifications.VerifyResultUrl2(MakeJws(rsa, "other-shop"), cert));
        Assert.Throws<RobokassaException>(() => t.Client.Notifications.VerifyResultUrl2(jws[..^4] + "aaaa", cert));
        Assert.Throws<RobokassaException>(() => t.Client.Notifications.VerifyResultUrl2(jws, cert, expectedAlgorithm: "none"));
        Assert.Throws<RobokassaException>(() => t.Client.Notifications.VerifyResultUrl2(jws, cert, maxAge: TimeSpan.FromMinutes(5), now: DateTimeOffset.UtcNow.AddDays(1)));
        using var other = RSA.Create(2048);
        using var otherCert = new CertificateRequest("CN=wrong", other, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Throws<RobokassaException>(() => t.Client.Notifications.VerifyResultUrl2(jws, otherCert));
    }

    private static string MakeJws(RSA rsa, string shop)
    {
        var header = Inspect.B64("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
        var payload = Inspect.B64(JsonSerializer.Serialize(new
        {
            header = new { type = "PaymentStateNotification", version = "1.0.0", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
            data = new { shop, opKey = "op", invId = "42", paymentMethod = "BankCard", incSum = "100.00", state = "HOLD" }
        }));
        var input = header + "." + payload;
        return input + "." + Inspect.B64(rsa.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    private static Dictionary<string, string> Fields() => new()
    {
        ["OutSum"] = "100.000000", ["InvId"] = "42", ["SignatureValue"] = "4E4AFAA4B39D1963FD295097C49375E3",
        ["Shp_b"] = "2", ["Shp_a"] = "заказ"
    };
}
