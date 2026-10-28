using System.Globalization;
using System.Text.Json;
using Robokassa.Models;
using Xunit;

namespace Robokassa.Tests;

public sealed class InvoiceTests
{
    [Fact]
    public async Task CreateUsesInvoiceJwtAndStringParameters()
    {
        using var t = new TestClient(test: true);
        t.Handler.Respond = async (r, cancellation) =>
        {
            Assert.Equal(HttpMethod.Post, r.Method);
            Assert.Equal("https://services.example/InvoiceServiceWebApi/api/CreateInvoice", r.RequestUri!.AbsoluteUri);
            var p = await Inspect.Jwt(r, "demo:test1");
            Assert.Equal("demo", p.GetProperty("MerchantLogin").GetString());
            Assert.Equal(long.MaxValue, p.GetProperty("InvId").GetInt64());
            Assert.Equal(99.99m, p.GetProperty("OutSum").GetDecimal());
            Assert.Equal("OneTime", p.GetProperty("InvoiceType").GetString());
            var a = p.GetProperty("AdditionalParameters");
            Assert.All(a.EnumerateObject(), property => Assert.Equal(JsonValueKind.String, property.Value.ValueKind));
            Assert.Equal("1", a.GetProperty("IsTest").GetString());
            Assert.Equal("true", a.GetProperty("StepByStep").GetString());
            Assert.Equal("https://example.com/cb?a=1&b=2", a.GetProperty("ResultURL2").GetString());
            Assert.Equal("[\"BankCard\",\"SBP\"]", a.GetProperty("PaymentMethods").GetString());
            Assert.False(p.TryGetProperty("Password1", out _));
            return StubHandler.Json(TestClient.InvoiceResponse);
        };
        var response = await t.Client.Invoices.CreateAsync(new CreateInvoiceRequest
        {
            InvId = long.MaxValue, OutSum = 99.99m,
            AdditionalParameters = new() { StepByStep = true, ResultUrl2 = "https://example.com/cb?a=1&b=2", PaymentMethods = ["BankCard", "SBP"] }
        });
        Assert.Equal(42, response.InvId);
        Assert.Equal(1, t.Handler.Calls);
    }

    [Theory]
    [InlineData(RobokassaHashAlgorithm.Md5, "MD5")]
    [InlineData(RobokassaHashAlgorithm.Sha1, "HS1")]
    [InlineData(RobokassaHashAlgorithm.Sha256, "HS256")]
    [InlineData(RobokassaHashAlgorithm.Sha384, "HS384")]
    [InlineData(RobokassaHashAlgorithm.Sha512, "HS512")]
    public async Task JwtMatchesIndependentHmac(RobokassaHashAlgorithm algorithm, string header)
    {
        using var t = new TestClient(algorithm: algorithm);
        t.Handler.Respond = async (r, cancellation) => { await Inspect.Jwt(r, expectedAlg: header); return StubHandler.Json(TestClient.InvoiceResponse); };
        await t.Client.Invoices.CreateAsync(new CreateInvoiceRequest { OutSum = 1m });
    }

    [Fact]
    public async Task ItemOnlyInvoiceAllowsAutomaticNumberAndNoOutSum()
    {
        using var t = new TestClient();
        t.Handler.Respond = async (r, cancellation) =>
        {
            var p = await Inspect.Jwt(r);
            Assert.False(p.TryGetProperty("InvId", out _));
            Assert.False(p.TryGetProperty("OutSum", out _));
            Assert.False(p.TryGetProperty("AdditionalParameters", out _));
            Assert.Equal(1.25m, p.GetProperty("InvoiceItems")[0].GetProperty("Quantity").GetDecimal());
            return StubHandler.Json(TestClient.InvoiceResponse);
        };
        await t.Client.Invoices.CreateAsync(new CreateInvoiceRequest
        { InvoiceItems = [new() { Name = "услуга", Quantity = 1.25m, Cost = 20m, Tax = "none" }] });
    }

    [Theory]
    [InlineData("StepByStep")]
    [InlineData("Recurring")]
    [InlineData("Token")]
    [InlineData("Split")]
    public async Task SpecialScenariosStayOnInvoiceApi(string scenario)
    {
        using var t = new TestClient();
        var parameters = scenario switch
        {
            "StepByStep" => new InvoiceParameters { StepByStep = true },
            "Recurring" => new InvoiceParameters { Recurring = true },
            "Token" => new InvoiceParameters { Token = "saved-card" },
            _ => new InvoiceParameters { Split = "{\"merchants\":[]}" }
        };
        t.Handler.Respond = async (r, cancellation) =>
        {
            Assert.EndsWith("CreateInvoice", r.RequestUri!.AbsolutePath);
            var p = await Inspect.Jwt(r);
            Assert.True(p.GetProperty("AdditionalParameters").TryGetProperty(scenario, out _));
            return StubHandler.Json(TestClient.InvoiceResponse);
        };
        await t.Client.Invoices.CreateAsync(new CreateInvoiceRequest { OutSum = 1m, AdditionalParameters = parameters });
    }

    [Fact]
    public async Task AllManagementMethodsUsePrimaryPasswordAndExactFieldCasing()
    {
        using var t = new TestClient(test: true);
        t.Handler.Respond = async (r, cancellation) =>
        {
            var p = await Inspect.Jwt(r);
            Assert.False(p.TryGetProperty("AdditionalParameters", out _));
            if (r.RequestUri!.AbsolutePath.EndsWith("GetInvoiceInformationList"))
            {
                Assert.Equal("NotPaid", p.GetProperty("InvoiceStatuses")[0].GetString());
                Assert.Equal("Reusable", p.GetProperty("InvoiceTypes")[0].GetString());
                return StubHandler.Json("""{"isSuccess":true,"invoiceInformationList":[],"total":0}""");
            }
            if (r.RequestUri.AbsolutePath.EndsWith("GetInvoiceInformation"))
                return StubHandler.Json("""{"isSuccess":true,"invoiceInformation":{"id":"id","invId":42,"invoiceType":"OneTime","created":"2026-01-01T00:00:00Z","modified":"2026-01-01T00:00:00Z","outSum":100,"invoiceItems":[],"isCustomerNotificationEmailSent":false,"invoiceStatus":"NotPaid","aliases":[],"userFields":{},"payments":[],"isWithoutFreeSale":false}}""");
            Assert.Equal(42, p.GetProperty("InvId").GetInt64());
            return StubHandler.Json("{\"isSuccess\":true}");
        };
        await t.Client.Invoices.DeactivateAsync(new() { InvId = 42 });
        var invoice = await t.Client.Invoices.GetAsync(new() { InvId = 42 });
        Assert.Equal("NotPaid", invoice.InvoiceInformation.InvoiceStatus);
        await t.Client.Invoices.NotifyCustomerAsync(new() { InvId = 42, CustomerNotificationEmail = "buyer@example.com" });
        await t.Client.Invoices.ListAsync(new()
        { CurrentPage = 1, PageSize = 10, InvoiceStatuses = ["NotPaid"], InvoiceTypes = ["Reusable"], DateFrom = DateTimeOffset.UtcNow.AddDays(-1), DateTo = DateTimeOffset.UtcNow });
        Assert.Equal(4, t.Handler.Calls);
    }

    [Fact]
    public async Task InvalidParametersFailBeforeNetwork()
    {
        using var t = new TestClient(test: true);
        await Assert.ThrowsAsync<ArgumentException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = -1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => t.Client.Invoices.CreateAsync(new()));
        await Assert.ThrowsAsync<ArgumentException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 1, AdditionalParameters = new() { StepByStep = true, Token = "x" } }));
        await Assert.ThrowsAsync<ArgumentException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 1, AdditionalParameters = new() { Split = "{}" } }));
        await Assert.ThrowsAsync<ArgumentException>(() => t.Client.Invoices.DeactivateAsync(new() { InvId = 42, EncodedId = "x" }));
        await Assert.ThrowsAsync<ArgumentException>(() => t.Client.Invoices.GetAsync(new()));
        Assert.Equal(0, t.Handler.Calls);
    }

    [Fact]
    public async Task DecimalSerializationDoesNotDependOnCurrentCulture()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            using var t = new TestClient();
            t.Handler.Respond = async (r, cancellation) =>
            {
                var p = await Inspect.Jwt(r);
                Assert.Equal("100.25", p.GetProperty("OutSum").GetRawText());
                return StubHandler.Json(TestClient.InvoiceResponse);
            };
            await t.Client.Invoices.CreateAsync(new() { OutSum = 100.25m });
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
}
