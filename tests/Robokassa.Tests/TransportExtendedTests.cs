using System.Net;
using System.Text.Json;
using Robokassa.Models;
using Xunit;

namespace Robokassa.Tests;

public sealed class TransportExtendedTests
{
    [Theory]
    [InlineData("{\"isSuccess\":false,\"message\":\"password secret\"}", RobokassaErrorKind.Api)]
    [InlineData("{\"isSuccess\":true}", RobokassaErrorKind.InvalidResponse)]
    [InlineData("<html>secret</html>", RobokassaErrorKind.InvalidResponse)]
    [InlineData("null", RobokassaErrorKind.InvalidResponse)]
    [InlineData("{}", RobokassaErrorKind.InvalidResponse)]
    public async Task ApplicationAndProtocolErrorsAreSanitized(string body, RobokassaErrorKind expected)
    {
        using var t = new TestClient();
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json(body));
        var ex = await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 100 }));
        Assert.Equal(expected, ex.Kind);
        Assert.DoesNotContain("secret", ex.ToString());
        Assert.Null(ex.InnerException);
        Assert.Equal(1, t.Handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task HttpErrorsAreNeverRetried(HttpStatusCode status)
    {
        using var t = new TestClient();
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json("secret", status));
        var ex = await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 1 }));
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(RobokassaErrorKind.Http, ex.Kind);
        Assert.Equal(1, t.Handler.Calls);
    }

    [Fact]
    public async Task TimeoutAndCallerCancellationAreDistinct()
    {
        using var t = new TestClient(timeout: TimeSpan.FromMilliseconds(30));
        t.Handler.Respond = async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return StubHandler.Json("{}"); };
        var timeout = await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 1 }));
        Assert.Equal(RobokassaErrorKind.Timeout, timeout.Kind);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 1 }, source.Token));
    }

    [Fact]
    public async Task NetworkErrorsDoNotLeakInnerExceptionUrlOrRequest()
    {
        using var t = new TestClient();
        t.Handler.Respond = (_, _) => throw new HttpRequestException("https://secret.example/?jwt=secret");
        var ex = await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 1 }));
        Assert.Equal(RobokassaErrorKind.Transport, ex.Kind);
        Assert.DoesNotContain("secret", ex.ToString());
    }

    [Fact]
    public async Task ResponseSizeIsLimited()
    {
        using var t = new TestClient(maxBytes: 8);
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json(new string('x', 20)));
        var ex = await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Invoices.CreateAsync(new() { OutSum = 1 }));
        Assert.Equal(RobokassaErrorKind.InvalidResponse, ex.Kind);
    }

    [Fact]
    public async Task ExternalHttpClientRemainsUsableAfterSdkDisposal()
    {
        using var handler = new StubHandler();
        using var http = new HttpClient(handler);
        var sdk = new RobokassaClient(new() { MerchantLogin = "demo" }, http);
        sdk.Dispose();
        using var response = await http.GetAsync("https://example.com");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HoldUsesExactSignatureAndRejectsFalseWithoutRetry(bool confirm)
    {
        using var t = new TestClient();
        t.Handler.Respond = async (r, cancellation) =>
        {
            var form = Inspect.Form(await r.Content!.ReadAsStringAsync());
            Assert.Equal("42", form["InvoiceID"]);
            Assert.Equal("100.00", form["OutSum"]);
            Assert.Equal(confirm ? "c76b1b2ce919d2475134b5de5b379618" : "edf15d857a763e80d93af4e07a94845b", form["SignatureValue"]);
            return StubHandler.Json("\"success: false\"");
        };
        var ex = await Assert.ThrowsAsync<RobokassaException>(() => confirm ? t.Client.Holding.ConfirmAsync(42, "100.00") : t.Client.Holding.CancelAsync(42, "100.00"));
        Assert.Equal(RobokassaErrorKind.Api, ex.Kind);
        Assert.Equal(1, t.Handler.Calls);
    }

    [Fact]
    public async Task HoldSuccessMeansAccepted()
    {
        using var t = new TestClient();
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json("\"success: true\""));
        await t.Client.Holding.ConfirmAsync(42, "100.00");
        Assert.Equal(1, t.Handler.Calls);
    }

    [Fact]
    public async Task LiveOnlyMethodsFailInTestMode()
    {
        using var t = new TestClient(test: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Client.Holding.ConfirmAsync(42, "100"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Client.Holding.CancelAsync(42, "100"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Client.Refunds.CreateAsync(new() { OpKey = "op" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Client.Recurring.CreateAsync(42, 1, "100"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Client.Reference.GetOperationStateAsync(42));
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.Client.Fiscalization.RegisterAsync(new() { ShopLocator = new() { Identifier = "demo" }, Receipts = [] }));
        Assert.Equal(0, t.Handler.Calls);
    }

    [Fact]
    public async Task RefundUsesPassword3AndStateUsesIdQuery()
    {
        using var t = new TestClient();
        var id = Guid.NewGuid();
        t.Handler.Respond = async (r, cancellation) =>
        {
            if (r.Method == HttpMethod.Post)
            {
                var payload = await Inspect.Jwt(r, "refund", "HS256");
                Assert.Equal("op", payload.GetProperty("OpKey").GetString());
                Assert.Equal(1.25m, payload.GetProperty("RefundSum").GetDecimal());
                Assert.False(payload.TryGetProperty("MerchantLogin", out _));
                return StubHandler.Json($$"""{"success":true,"requestId":"{{id}}"}""");
            }
            Assert.Equal("?id=" + id, r.RequestUri!.Query);
            return StubHandler.Json($$"""{"requestId":"{{id}}","amount":1.25,"label":"completed"}""");
        };
        var created = await t.Client.Refunds.CreateAsync(new() { OpKey = "op", RefundSum = 1.25m });
        Assert.Equal(id.ToString(), created.RequestId);
        Assert.Equal(1.25m, (await t.Client.Refunds.GetStateAsync(id)).Amount);
    }

    [Fact]
    public async Task RecurringRequiresExactAcknowledgementAndExcludesParentFromSignature()
    {
        using var t = new TestClient();
        t.Handler.Respond = async (r, cancellation) =>
        {
            var f = Inspect.Form(await r.Content!.ReadAsStringAsync());
            Assert.Equal("3", f["PreviousInvoiceID"]);
            Assert.Equal("c76b1b2ce919d2475134b5de5b379618", f["SignatureValue"]);
            return StubHandler.Json("OK42");
        };
        Assert.Equal("OK42", await t.Client.Recurring.CreateAsync(42, 3, "100.00"));
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json("OK43"));
        await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Recurring.CreateAsync(42, 3, "100.00"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    public async Task SmsSupportsBothResultFormats(string result)
    {
        using var t = new TestClient();
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json("{\"result\":" + result + ",\"count\":3}"));
        Assert.Equal(3, (await t.Client.Messaging.SendAsync("79990000000", "тест")).Count);
    }

    [Fact]
    public async Task FiscalRegistrationUsesInvoiceJwtContractAndChecksIndividualResults()
    {
        using var t = new TestClient();
        t.Handler.Respond = async (r, cancellation) =>
        {
            Assert.Equal("/Fiscalization.Api.External/Receipt/Register", r.RequestUri!.AbsolutePath);
            var p = await Inspect.Jwt(r);
            Assert.Equal("demo", p.GetProperty("ShopLocator").GetProperty("Identifier").GetString());
            Assert.False(p.TryGetProperty("MerchantLogin", out _));
            return StubHandler.Json("""{"IsSuccess":true,"Data":{"ReceiptRegistrationResults":[{"IsSuccess":false,"OpId":42}]}}""");
        };
        await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Fiscalization.RegisterAsync(new()
        {
            ShopLocator = new() { Identifier = "demo" },
            Receipts = [new() { OpId = 42, Type = "Sell", Items = [new() { Name = "услуга", Quantity = 1, Sum = 100, VatType = "None", PaymentMethod = "FullPayment", PaymentObject = "Service" }], Payments = [new() { Type = "Cashless", Sum = 100 }] }]
        }));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" xmlns=\"http://merchant.roboxchange.com/WebService/\"")]
    public async Task XmlParsesNamespacesAndAttributes(string ns)
    {
        using var t = new TestClient();
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json($"<OperationStateResponse{ns}><Result><Code>0</Code></Result><State><Code>100</Code></State><Info><OutSum>100.000000</OutSum><OpKey>op</OpKey></Info></OperationStateResponse>"));
        var status = await t.Client.Reference.GetOperationStateAsync(42);
        Assert.Equal(100, status.StateCode);
        Assert.Equal("100.000000", status.OutSum);
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json($"<CurrenciesList{ns}><Result><Code>0</Code></Result><Groups><Group Code=\"BankCard\"><Items><Currency Label=\"BankCardPSR\" Alias=\"BankCard\" Name=\"карта\"/></Items></Group></Groups></CurrenciesList>"));
        Assert.Equal("BankCardPSR", (await t.Client.Reference.GetCurrenciesAsync())[0].Items[0].Label);
    }

    [Fact]
    public async Task XmlErrorsAndDtdAreRejected()
    {
        using var t = new TestClient();
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json("<OperationStateResponse><Result><Code>3</Code></Result></OperationStateResponse>"));
        Assert.Equal(3, (await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Reference.GetOperationStateAsync(42))).ApiCode);
        t.Handler.Respond = (_, _) => Task.FromResult(StubHandler.Json("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///etc/passwd'>]><CurrenciesList><Result><Code>0</Code></Result>&e;</CurrenciesList>"));
        Assert.Equal(RobokassaErrorKind.InvalidResponse, (await Assert.ThrowsAsync<RobokassaException>(() => t.Client.Reference.GetCurrenciesAsync())).Kind);
    }

    [Fact]
    public async Task RipemdJwtMatchesIndependentPythonOpenSslVector()
    {
        using var t = new TestClient(algorithm: RobokassaHashAlgorithm.Ripemd160);
        t.Handler.Respond = async (r, cancellation) =>
        {
            var jwt = JsonSerializer.Deserialize<string>(await r.Content!.ReadAsStringAsync());
            Assert.Equal("eyJ0eXAiOiJKV1QiLCJhbGciOiJSSVBFTUQxNjAifQ.eyJJbnZvaWNlVHlwZSI6Ik9uZVRpbWUiLCJDdWx0dXJlIjoicnUiLCJPdXRTdW0iOjEsIk1lcmNoYW50TG9naW4iOiJkZW1vIn0.sqqlGqwgb33AKZMt3yl2Bfw8Kws", jwt);
            return StubHandler.Json(TestClient.InvoiceResponse);
        };
        await t.Client.Invoices.CreateAsync(new() { OutSum = 1 });
    }
}
