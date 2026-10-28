using System.Text.Json;
using Robokassa.Models;

namespace Robokassa;

public sealed class HoldingService
{
    private readonly RobokassaOptions options;
    private readonly Transport transport;
    internal HoldingService(RobokassaOptions options, Transport transport) { this.options = options; this.transport = transport; }

    public Task ConfirmAsync(long invoiceId, string outSum, Receipt? receipt = null, CancellationToken cancellationToken = default) =>
        SendAsync(true, invoiceId, outSum, receipt, cancellationToken);
    public Task CancelAsync(long invoiceId, string outSum, CancellationToken cancellationToken = default) =>
        SendAsync(false, invoiceId, outSum, null, cancellationToken);

    private async Task SendAsync(bool confirm, long invoiceId, string sum, Receipt? receipt, CancellationToken ct)
    {
        options.RequireProduction();
        Wire.Positive(invoiceId);
        Wire.Amount(sum);
        var encodedReceipt = ClassicPaymentService.EncodeReceipt(receipt);
        var parts = new List<string> { options.MerchantLogin, confirm ? sum : "", Wire.Number(invoiceId) };
        if (encodedReceipt is not null) parts.Add(encodedReceipt);
        parts.Add(options.PaymentPassword);
        var fields = new Dictionary<string, string>
        {
            ["MerchantLogin"] = options.MerchantLogin, ["InvoiceID"] = Wire.Number(invoiceId), ["OutSum"] = sum,
            ["SignatureValue"] = RobokassaSignature.Hash(string.Join(":", parts), options.HashAlgorithm)
        };
        if (encodedReceipt is not null) fields.Add("Receipt", encodedReceipt);
        var operation = confirm ? "holding.confirm" : "holding.cancel";
        var body = await transport.SendAsync(operation, HttpMethod.Post,
            options.Endpoints.Payment(confirm ? "Merchant/Payment/Confirm" : "Merchant/Payment/Cancel"), new FormUrlEncodedContent(fields), ct).ConfigureAwait(false);
        string response;
        try { response = JsonSerializer.Deserialize<string>(body) ?? ""; }
        catch (JsonException) { response = body.Trim(); }
        if (response == "success: false") throw new RobokassaException(RobokassaErrorKind.Api, operation);
        if (response != "success: true") throw new RobokassaException(RobokassaErrorKind.InvalidResponse, operation);
    }
}

public sealed class RefundService
{
    private readonly RobokassaOptions options;
    private readonly Transport transport;
    internal RefundService(RobokassaOptions options, Transport transport) { this.options = options; this.transport = transport; }

    public Task<RefundCreateResponse> CreateAsync(CreateRefundRequest request, CancellationToken cancellationToken = default)
    {
        options.RequireProduction();
        ArgumentNullException.ThrowIfNull(request);
        RobokassaOptions.Require(request.OpKey, nameof(request.OpKey));
        if (request.RefundSum <= 0) throw new ArgumentException("сумма возврата должна быть положительной");
        InvoiceService.ValidateItems(request.InvoiceItems);
        return transport.JwtAsync<RefundCreateResponse>("refunds.create", options.Endpoints.Service("RefundService/Refund/Create"), request,
            RobokassaOptions.Require(options.Password3, nameof(options.Password3)), cancellationToken, "success", options.RefundHashAlgorithm);
    }

    public Task<RefundState> GetStateAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("нужен requestId");
        return transport.JsonAsync<RefundState>("refunds.getState", HttpMethod.Get,
            Wire.WithQuery(options.Endpoints.Service("RefundService/Refund/GetState"), new Dictionary<string, string> { ["id"] = requestId.ToString() }), null, cancellationToken);
    }
}

public sealed class RecurringService
{
    private readonly RobokassaOptions options;
    private readonly Transport transport;
    internal RecurringService(RobokassaOptions options, Transport transport) { this.options = options; this.transport = transport; }

    public async Task<string> CreateAsync(long invoiceId, long previousInvoiceId, string outSum, string? description = null,
        Receipt? receipt = null, IReadOnlyDictionary<string, string>? shpFields = null, CancellationToken cancellationToken = default)
    {
        options.RequireProduction();
        Wire.Positive(invoiceId);
        Wire.Positive(previousInvoiceId);
        Wire.Amount(outSum);
        var encoded = ClassicPaymentService.EncodeReceipt(receipt);
        var parts = new List<string> { options.MerchantLogin, outSum, Wire.Number(invoiceId) };
        if (encoded is not null) parts.Add(encoded);
        parts.Add(options.PaymentPassword);
        parts.AddRange(Wire.Shp(shpFields));
        var fields = new Dictionary<string, string>
        {
            ["MerchantLogin"] = options.MerchantLogin, ["InvoiceID"] = Wire.Number(invoiceId),
            ["PreviousInvoiceID"] = Wire.Number(previousInvoiceId), ["OutSum"] = outSum,
            ["SignatureValue"] = RobokassaSignature.Hash(string.Join(":", parts), options.HashAlgorithm)
        };
        if (encoded is not null) fields.Add("Receipt", encoded);
        if (description is not null) fields.Add("Description", description);
        foreach (var p in shpFields ?? new Dictionary<string, string>()) fields.Add(p.Key, p.Value);
        var body = await transport.SendAsync("recurring.create", HttpMethod.Post, options.Endpoints.Payment("Merchant/Recurring"),
            new FormUrlEncodedContent(fields), cancellationToken).ConfigureAwait(false);
        if (body.Trim() != "OK" + Wire.Number(invoiceId)) throw new RobokassaException(RobokassaErrorKind.Api, "recurring.create");
        return body.Trim();
    }
}

public sealed class FiscalizationService
{
    private readonly RobokassaOptions options;
    private readonly Transport transport;
    internal FiscalizationService(RobokassaOptions options, Transport transport) { this.options = options; this.transport = transport; }

    public async Task<FiscalReceiptRegisterResponse> RegisterAsync(RegisterFiscalReceiptRequest request, CancellationToken cancellationToken = default)
    {
        options.RequireProduction();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ShopLocator);
        var locator = request.ShopLocator;
        if ((locator.MrhId is null ? 0 : 1) + (locator.Id is null ? 0 : 1) + (locator.Identifier is null ? 0 : 1) != 1 ||
            request.Receipts is not { Count: > 0 }) throw new ArgumentException("нужны магазин и чеки");
        foreach (var receipt in request.Receipts)
        {
            if (receipt.OpId <= 0 || receipt.Type != "Sell" || receipt.Items is not { Count: > 0 } || receipt.Payments is not { Count: > 0 } ||
                receipt.Items.Any(i => string.IsNullOrWhiteSpace(i.Name) || i.Quantity <= 0 || i.Sum is null && i.Cost is null || i.Sum < 0 || i.Cost < 0) ||
                receipt.Payments.Any(p => p.Sum < 0)) throw new ArgumentException("некорректный чек");
        }
        var response = await transport.JwtAsync<FiscalReceiptRegisterResponse>("fiscalization.register", options.Endpoints.Service("Fiscalization.Api.External/Receipt/Register"),
            request, options.MerchantLogin + ":" + RobokassaOptions.Require(options.Password1, nameof(options.Password1)), cancellationToken, "IsSuccess").ConfigureAwait(false);
        if (response.Data?.ReceiptRegistrationResults?.Any(r => !r.IsSuccess) == true)
            throw new RobokassaException(RobokassaErrorKind.Api, "fiscalization.register");
        return response;
    }
}

public sealed class MessagingService
{
    private readonly RobokassaOptions options;
    private readonly Transport transport;
    internal MessagingService(RobokassaOptions options, Transport transport) { this.options = options; this.transport = transport; }

    public async Task<SmsResponse> SendAsync(string phone, string message, bool useGet = false, CancellationToken cancellationToken = default)
    {
        options.RequireProduction();
        RobokassaOptions.Require(phone, nameof(phone));
        RobokassaOptions.Require(message, nameof(message));
        if (message.Length > 128) throw new ArgumentException("длина sms превышает 128 символов");
        var fields = new Dictionary<string, string>
        {
            ["login"] = options.MerchantLogin, ["phone"] = phone, ["message"] = message,
            ["signature"] = RobokassaSignature.Hash($"{options.MerchantLogin}:{phone}:{message}:{options.PaymentPassword}", options.HashAlgorithm)
        };
        var uri = options.Endpoints.Service("SMS/");
        var response = await transport.JsonAsync<SmsResponse>("messaging.send", useGet ? HttpMethod.Get : HttpMethod.Post,
            useGet ? Wire.WithQuery(uri, fields) : uri, useGet ? null : new FormUrlEncodedContent(fields), cancellationToken).ConfigureAwait(false);
        var accepted = response.Result.ValueKind == JsonValueKind.True ||
            response.Result.ValueKind == JsonValueKind.Number && response.Result.TryGetInt32(out var code) && code == 1;
        if (!accepted) throw new RobokassaException(RobokassaErrorKind.Api, "messaging.send", apiCode: response.ErrorCode);
        return response;
    }
}
