using System.Text.Json;
using Robokassa.Models;

namespace Robokassa;

public sealed class ClassicPaymentService
{
    private readonly RobokassaOptions options;
    internal ClassicPaymentService(RobokassaOptions options) { this.options = options; }

    public Uri BuildPaymentUrl(PaymentRequest request) => Wire.WithQuery(options.Endpoints.Payment("Merchant/Index.aspx"), Prepare(request));
    public PaymentForm BuildPaymentForm(PaymentRequest request) => new(options.Endpoints.Payment("Merchant/Index.aspx"), Prepare(request));

    public Uri BuildSavedCardUrl(PaymentRequest request)
    {
        options.RequireProduction();
        RobokassaOptions.Require(request.Token, nameof(request.Token));
        return Wire.WithQuery(options.Endpoints.Payment("Merchant/Payment/CoFPayment"), Prepare(request));
    }
    public PaymentForm BuildSavedCardForm(PaymentRequest request)
    {
        options.RequireProduction();
        RobokassaOptions.Require(request.Token, nameof(request.Token));
        return new(options.Endpoints.Payment("Merchant/Payment/CoFPayment"), Prepare(request));
    }

    public Uri BuildSplitUrl(SplitPaymentInvoice invoice) => Wire.WithQuery(options.Endpoints.Payment("Merchant/Payment/CreateV2"), SplitFields(invoice));
    public PaymentForm BuildSplitForm(SplitPaymentInvoice invoice) => new(options.Endpoints.Payment("Merchant/Payment/CreateV2"), SplitFields(invoice));

    private IReadOnlyList<KeyValuePair<string, string>> SplitFields(SplitPaymentInvoice invoice)
    {
        options.RequireProduction();
        ArgumentNullException.ThrowIfNull(invoice);
        if (invoice.OutAmount <= 0 || invoice.Merchant?.Id != options.MerchantLogin || invoice.SplitMerchants is not { Count: > 0 } ||
            invoice.SplitMerchants.Any(p => string.IsNullOrWhiteSpace(p.Id) || p.Amount < 0 || p.InvoiceId < 0))
            throw new ArgumentException("некорректный сплит");
        var json = JsonSerializer.Serialize(invoice, Wire.Json);
        return new Dictionary<string, string>
        {
            ["invoice"] = Wire.Encode(json),
            ["signature"] = RobokassaSignature.Hash(json + options.PaymentPassword, options.HashAlgorithm)
        }.ToArray();
    }

    private IReadOnlyList<KeyValuePair<string, string>> Prepare(PaymentRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        Wire.Amount(r.OutSum);
        if (r.InvId < 0 || r.Description?.Length > 100 || r.Culture is not null and not ("ru" or "en"))
            throw new ArgumentException("некорректные параметры платежа");
        if ((r.StepByStep == true ? 1 : 0) + (r.Recurring == true ? 1 : 0) + (r.Token is not null ? 1 : 0) > 1)
            throw new ArgumentException("несовместимые параметры платежа");
        InvoiceService.ValidateRedirect(r.SuccessUrl2, r.SuccessUrl2Method);
        InvoiceService.ValidateRedirect(r.FailUrl2, r.FailUrl2Method);
        var receipt = EncodeReceipt(r.Receipt);
        var parts = new List<string> { options.MerchantLogin, r.OutSum, r.InvId is { } id ? Wire.Number(id) : "" };
        if (receipt is not null) parts.Add(receipt);
        if (r.StepByStep == true) parts.Add("true");
        foreach (var modifier in new[] { r.ResultUrl2, r.SuccessUrl2, r.SuccessUrl2Method, r.FailUrl2, r.FailUrl2Method, r.Token })
            if (!string.IsNullOrEmpty(modifier)) parts.Add(Wire.Encode(modifier));
        parts.Add(options.PaymentPassword);
        parts.AddRange(Wire.Shp(r.ShpFields));
        var fields = new List<KeyValuePair<string, string>>();
        void Add(string key, string? value) { if (value is not null) fields.Add(new(key, value)); }
        Add("MerchantLogin", options.MerchantLogin);
        Add("OutSum", r.OutSum);
        Add("InvId", r.InvId is { } invoiceId ? Wire.Number(invoiceId) : null);
        Add("SignatureValue", RobokassaSignature.Hash(string.Join(":", parts), options.HashAlgorithm));
        Add("Description", r.Description);
        Add("Culture", r.Culture);
        Add("Encoding", "UTF-8");
        Add("IsTest", options.IsTest ? "1" : null);
        Add("Receipt", receipt);
        Add("Email", r.Email);
        Add("IncCurrLabel", r.IncCurrLabel);
        Add("ExpirationDate", r.ExpirationDate);
        Add("StepByStep", r.StepByStep?.ToString().ToLowerInvariant());
        Add("Recurring", r.Recurring?.ToString().ToLowerInvariant());
        Add("IsWithoutFreeSale", r.IsWithoutFreeSale?.ToString().ToLowerInvariant());
        Add("FiscalParentOpId", r.FiscalParentOpId is { } parent ? Wire.Number(parent) : null);
        foreach (var pair in new Dictionary<string, string?>
        {
            ["ResultUrl2"] = r.ResultUrl2, ["SuccessUrl2"] = r.SuccessUrl2, ["SuccessUrl2Method"] = r.SuccessUrl2Method,
            ["FailUrl2"] = r.FailUrl2, ["FailUrl2Method"] = r.FailUrl2Method, ["Token"] = r.Token
        }) Add(pair.Key, pair.Value is null ? null : Wire.Encode(pair.Value));
        foreach (var method in r.PaymentMethods ?? []) Add("PaymentMethods", method);
        foreach (var pair in r.ShpFields ?? new Dictionary<string, string>()) Add(pair.Key, pair.Value);
        return fields.AsReadOnly();
    }

    internal static string? EncodeReceipt(Receipt? receipt)
    {
        if (receipt is null) return null;
        if (receipt.Items is not { Count: > 0 } || receipt.Items.Any(i => i is null || string.IsNullOrWhiteSpace(i.Name) ||
            i.Name.Length > 128 || i.Quantity <= 0 || i.Sum < 0 || string.IsNullOrWhiteSpace(i.Tax)))
            throw new ArgumentException("некорректный чек");
        return Wire.Encode(JsonSerializer.Serialize(receipt, Wire.Json));
    }
}
