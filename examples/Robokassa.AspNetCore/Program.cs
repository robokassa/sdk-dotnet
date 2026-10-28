using Robokassa;
using Robokassa.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var options = new RobokassaOptions
{
    MerchantLogin = config["ROBOKASSA_LOGIN"] ?? throw new InvalidOperationException("нужен ROBOKASSA_LOGIN"),
    Password1 = config["ROBOKASSA_PASSWORD1"],
    Password2 = config["ROBOKASSA_PASSWORD2"],
    TestPassword1 = config["ROBOKASSA_TEST_PASSWORD1"],
    TestPassword2 = config["ROBOKASSA_TEST_PASSWORD2"],
    IsTest = config["ROBOKASSA_IS_TEST"] != "0",
    HashAlgorithm = Enum.Parse<RobokassaHashAlgorithm>(config["ROBOKASSA_HASH_ALGORITHM"] ?? "Md5", ignoreCase: true)
};
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<DemoOrderStore>();
builder.Services.AddHttpClient("robokassa").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddTransient(sp => new RobokassaClient(options, sp.GetRequiredService<IHttpClientFactory>().CreateClient("robokassa")));
var app = builder.Build();

app.MapGet("/", () => Results.Text("тестовый пример: POST /orders, POST /robokassa/result, GET /orders/{id}"));

app.MapPost("/orders", async (RobokassaClient client, DemoOrderStore orders, CancellationToken ct) =>
{
    var order = orders.Create(100m);
    try
    {
        var invoice = await client.Invoices.CreateAsync(new CreateInvoiceRequest
        {
            InvId = order.Id, OutSum = order.Amount, Description = "тестовый заказ",
            InvoiceItems = [new() { Name = "услуга", Quantity = 1, Cost = order.Amount, Tax = "none", PaymentMethod = "full_payment", PaymentObject = "service" }]
        }, ct);
        return Results.Ok(new { orderId = order.Id, invoice.Id, invoice.InvId, invoice.Url });
    }
    catch (RobokassaException ex)
    {
        // при таймауте счет мог создаться, сначала проверьте его по order.Id
        return Results.Problem(statusCode: 502, title: "не удалось получить счет", extensions: new Dictionary<string, object?>
        { ["orderId"] = order.Id, ["kind"] = ex.Kind.ToString() });
    }
});

app.MapGet("/orders/{id:long}", (long id, DemoOrderStore orders) => orders.Get(id) is { } order ? Results.Ok(order) : Results.NotFound());

app.MapPost("/robokassa/result", async (HttpRequest request, RobokassaClient client, DemoOrderStore orders, CancellationToken ct) =>
{
    if (!request.HasFormContentType) return Results.BadRequest();
    var form = await request.ReadFormAsync(ct);
    if (form.Any(p => p.Value.Count != 1)) return Results.BadRequest();
    try
    {
        var notification = client.Notifications.VerifyResultUrl(form.Select(p => new KeyValuePair<string, string>(p.Key, p.Value[0]!)));
        if (!orders.MarkPaid(notification.InvoiceId, notification.Amount)) return Results.BadRequest();
        return Results.Text(NotificationService.ResultResponse(notification), "text/plain");
    }
    catch (RobokassaException) { return Results.BadRequest(); }
});

app.MapGet("/robokassa/success", () => Results.Text("результат оплаты проверяется сервером по уведомлению"));
app.MapGet("/robokassa/fail", () => Results.Text("оплата не завершена"));
app.Run();

public sealed record DemoOrder(long Id, decimal Amount, bool Paid);

public sealed class DemoOrderStore
{
    private readonly object gate = new();
    private readonly Dictionary<long, DemoOrder> orders = [];
    private long sequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public DemoOrder Create(decimal amount)
    {
        lock (gate)
        {
            var order = new DemoOrder(++sequence, amount, false);
            orders.Add(order.Id, order);
            return order;
        }
    }
    public DemoOrder? Get(long id) { lock (gate) return orders.GetValueOrDefault(id); }
    public bool MarkPaid(long id, decimal amount)
    {
        lock (gate)
        {
            if (!orders.TryGetValue(id, out var order) || order.Amount != amount) return false;
            if (!order.Paid) orders[id] = order with { Paid = true };
            return true;
        }
    }
}
