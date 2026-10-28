using System.Text.Json.Serialization;

namespace Robokassa.Models;

public sealed class PaymentRequest
{
    public required string OutSum { get; init; }
    public long? InvId { get; init; }
    public string? Description { get; init; }
    public string? Culture { get; init; }
    public string? Email { get; init; }
    public string? IncCurrLabel { get; init; }
    public string? ExpirationDate { get; init; }
    public Receipt? Receipt { get; init; }
    public bool? StepByStep { get; init; }
    public bool? Recurring { get; init; }
    public string? Token { get; init; }
    public string? ResultUrl2 { get; init; }
    public string? SuccessUrl2 { get; init; }
    public string? SuccessUrl2Method { get; init; }
    public string? FailUrl2 { get; init; }
    public string? FailUrl2Method { get; init; }
    public IReadOnlyList<string>? PaymentMethods { get; init; }
    public bool? IsWithoutFreeSale { get; init; }
    public long? FiscalParentOpId { get; init; }
    public IReadOnlyDictionary<string, string>? ShpFields { get; init; }
}

public sealed class Receipt
{
    [JsonPropertyName("items")]
    public required IReadOnlyList<ReceiptItem> Items { get; init; }
    [JsonPropertyName("sno")]
    public string? Sno { get; init; }
}

public sealed class ReceiptItem
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    [JsonPropertyName("quantity")]
    public required decimal Quantity { get; init; }
    [JsonPropertyName("sum")]
    public required decimal Sum { get; init; }
    [JsonPropertyName("tax")]
    public required string Tax { get; init; }
    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; init; }
    [JsonPropertyName("payment_object")]
    public string? PaymentObject { get; init; }
    [JsonPropertyName("nomenclature_code")]
    public string? NomenclatureCode { get; init; }
}

public sealed record PaymentForm(Uri Action, IReadOnlyList<KeyValuePair<string, string>> Fields)
{
    public string Method => "POST";
}

public sealed record OperationStatus(int StateCode, string? OutSum, string? OpKey,
    DateTimeOffset? RequestDate, DateTimeOffset? StateDate, IReadOnlyDictionary<string, string> Info,
    IReadOnlyDictionary<string, string> UserFields);

public sealed record PaymentMethod(string? Label, string? Alias, string? Name, string? MinValue, string? MaxValue);
public sealed record PaymentMethodGroup(string? Code, string? Description, IReadOnlyList<PaymentMethod> Items);
