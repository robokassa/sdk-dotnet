using System.Text.Json.Nodes;
using Robokassa.Models;

namespace Robokassa;

public sealed class InvoiceService
{
    private readonly RobokassaOptions options;
    private readonly Transport transport;
    internal InvoiceService(RobokassaOptions options, Transport transport) { this.options = options; this.transport = transport; }

    public async Task<CreateInvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreate(request);
        var payload = Wire.Payload(request, options.MerchantLogin);
        if (options.IsTest)
        {
            var additional = payload["AdditionalParameters"] as JsonObject ?? new JsonObject();
            additional["IsTest"] = "1";
            payload["AdditionalParameters"] = additional;
        }
        var response = await SendAsync<CreateInvoiceResponse>("CreateInvoice", payload, options.PaymentPassword, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(response.Id) || response.InvId <= 0 ||
            !Uri.TryCreate(response.Url, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            throw new RobokassaException(RobokassaErrorKind.InvalidResponse, "invoices.CreateInvoice");
        return response;
    }

    public async Task<string> CreatePaymentUrlAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default) =>
        (await CreateAsync(request, cancellationToken).ConfigureAwait(false)).Url;

    public Task<InvoiceActionResponse> DeactivateAsync(DeactivateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentifier(request.Id, request.InvId, request.EncodedId);
        return SendManagementAsync<InvoiceActionResponse>("DeactivateInvoice", request, cancellationToken);
    }

    public Task<GetInvoiceResponse> GetAsync(GetInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdentifier(request.Id, request.InvId, null);
        return SendManagementAsync<GetInvoiceResponse>("GetInvoiceInformation", request, cancellationToken);
    }

    public Task<InvoiceInformationListResponse> ListAsync(InvoiceFilter request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CurrentPage < 1 || request.PageSize < 1 || request.DateFrom > request.DateTo ||
            request.InvoiceStatuses is not { Count: > 0 } || request.InvoiceTypes is not { Count: > 0 } ||
            request.InvoiceStatuses.Any(s => s is not ("NotPaid" or "Paid" or "Expired")) ||
            request.InvoiceTypes.Any(s => s is not ("OneTime" or "Reusable")) ||
            request.SumFrom < 0 || request.SumTo < 0 || request.SumFrom > request.SumTo)
            throw new ArgumentException("некорректный фильтр счетов", nameof(request));
        return SendManagementAsync<InvoiceInformationListResponse>("GetInvoiceInformationList", request, cancellationToken);
    }

    public Task<InvoiceActionResponse> NotifyCustomerAsync(NotifyCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Wire.Positive(request.InvId);
        return SendManagementAsync<InvoiceActionResponse>("NotifyCustomer", request, cancellationToken);
    }

    private Task<T> SendManagementAsync<T>(string method, object request, CancellationToken ct) =>
        SendAsync<T>(method, Wire.Payload(request, options.MerchantLogin), RobokassaOptions.Require(options.Password1, nameof(options.Password1)), ct);

    private Task<T> SendAsync<T>(string method, object payload, string password, CancellationToken ct) =>
        transport.JwtAsync<T>("invoices." + method, options.Endpoints.Service("InvoiceServiceWebApi/api/" + method),
            payload, options.MerchantLogin + ":" + password, ct, "isSuccess");

    private static void ValidateIdentifier(string? id, long? invId, string? encodedId)
    {
        if ((id is null ? 0 : 1) + (invId is null ? 0 : 1) + (encodedId is null ? 0 : 1) != 1 ||
            id is not null && (!Guid.TryParse(id, out var guid) || guid == Guid.Empty) ||
            invId <= 0 || encodedId is not null && string.IsNullOrWhiteSpace(encodedId))
            throw new ArgumentException("нужен ровно один корректный идентификатор счета");
    }

    private void ValidateCreate(CreateInvoiceRequest r)
    {
        if (r.InvoiceType is not ("OneTime" or "Reusable") || r.Culture is not ("ru" or "en") ||
            r.InvId < 0 || r.OutSum <= 0 || r.OutSum is null && r.InvoiceItems is not { Count: > 0 })
            throw new ArgumentException("некорректные параметры счета");
        ValidateItems(r.InvoiceItems);
        if (r.SuccessUrl2Data is { } success) ValidateRedirect(success.Url, success.Method);
        if (r.FailUrl2Data is { } fail) ValidateRedirect(fail.Url, fail.Method);
        var p = r.AdditionalParameters;
        if (p is null) return;
        if ((p.StepByStep == true ? 1 : 0) + (p.Recurring == true ? 1 : 0) + (p.Token is not null ? 1 : 0) > 1)
            throw new ArgumentException("StepByStep, Recurring и Token несовместимы");
        if (options.IsTest && p.Split is not null)
            throw new ArgumentException("сплитование несовместимо с тестовым режимом");
        if (p.Token is not null && string.IsNullOrWhiteSpace(p.Token)) throw new ArgumentException("Token не может быть пустым");
        if (p.Split is not null)
        {
            try { if (JsonNode.Parse(p.Split) is not JsonObject) throw new System.Text.Json.JsonException(); }
            catch (System.Text.Json.JsonException) { throw new ArgumentException("Split должен содержать json-объект"); }
        }
        ValidateRedirect(p.SuccessUrl2, p.SuccessUrl2Method);
        ValidateRedirect(p.FailUrl2, p.FailUrl2Method);
        if (p.Culture is not null and not ("ru" or "en")) throw new ArgumentException("неверный язык");
    }

    internal static void ValidateItems(IReadOnlyList<InvoiceItem>? items)
    {
        if (items is null) return;
        if (items.Count == 0 || items.Any(i => i is null || string.IsNullOrWhiteSpace(i.Name) ||
            i.Quantity <= 0 || i.Cost < 0 || string.IsNullOrWhiteSpace(i.Tax)))
            throw new ArgumentException("некорректные позиции счета");
    }

    internal static void ValidateRedirect(string? url, string? method)
    {
        if (method is not null and not ("GET" or "POST") || method is not null && url is null)
            throw new ArgumentException("неверный метод переадресации");
        if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new ArgumentException("неверный url переадресации");
    }
}
