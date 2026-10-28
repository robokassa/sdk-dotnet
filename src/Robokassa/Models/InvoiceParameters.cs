using System.Text.Json;
using System.Text.Json.Serialization;

namespace Robokassa.Models;

[JsonConverter(typeof(InvoiceParametersConverter))]
public sealed class InvoiceParameters
{
    public string? Email { get; init; }
    public string? IncCurrLabel { get; init; }
    public IReadOnlyList<string>? PaymentMethods { get; init; }
    public string? ResultUrl2 { get; init; }
    public bool? StepByStep { get; init; }
    public bool? Recurring { get; init; }
    public string? Token { get; init; }
    public string? Split { get; init; }
    public string? Culture { get; init; }
    public string? SuccessUrl2 { get; init; }
    public string? SuccessUrl2Method { get; init; }
    public string? FailUrl2 { get; init; }
    public string? FailUrl2Method { get; init; }
    public bool? IsWithoutFreeSale { get; init; }
    public long? FiscalParentOpId { get; init; }
}

public sealed class InvoiceParametersConverter : JsonConverter<InvoiceParameters>
{
    public override InvoiceParameters Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("параметры предназначены для отправки запросов");

    public override void Write(Utf8JsonWriter writer, InvoiceParameters value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        void Write(string name, string? content) { if (content is not null) writer.WriteString(name, content); }
        static string? Flag(bool? flag) => flag?.ToString().ToLowerInvariant();
        Write("Email", value.Email);
        Write("IncCurrLabel", value.IncCurrLabel);
        Write("PaymentMethods", value.PaymentMethods is null ? null : JsonSerializer.Serialize(value.PaymentMethods, options));
        Write("ResultURL2", value.ResultUrl2);
        Write("StepByStep", Flag(value.StepByStep));
        Write("Recurring", Flag(value.Recurring));
        Write("Token", value.Token);
        Write("Split", value.Split);
        Write("Culture", value.Culture);
        Write("SuccessUrl2", value.SuccessUrl2);
        Write("SuccessUrl2Method", value.SuccessUrl2Method);
        Write("FailUrl2", value.FailUrl2);
        Write("FailUrl2Method", value.FailUrl2Method);
        Write("IsWithoutFreeSale", Flag(value.IsWithoutFreeSale));
        Write("FiscalParentOpId", value.FiscalParentOpId is { } id ? Wire.Number(id) : null);
        writer.WriteEndObject();
    }
}
