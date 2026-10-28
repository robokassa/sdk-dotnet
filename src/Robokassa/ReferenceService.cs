using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Robokassa.Models;

namespace Robokassa;

public sealed class ReferenceService
{
    private readonly RobokassaOptions options;
    private readonly Transport transport;
    internal ReferenceService(RobokassaOptions options, Transport transport) { this.options = options; this.transport = transport; }

    public async Task<OperationStatus> GetOperationStateAsync(long invoiceId, CancellationToken cancellationToken = default)
    {
        options.RequireProduction();
        Wire.Positive(invoiceId);
        const string operation = "reference.operationState";
        var fields = new Dictionary<string, string>
        {
            ["MerchantLogin"] = options.MerchantLogin, ["InvoiceID"] = Wire.Number(invoiceId),
            ["Signature"] = RobokassaSignature.Hash($"{options.MerchantLogin}:{Wire.Number(invoiceId)}:{options.NotificationPassword}", options.HashAlgorithm)
        };
        var root = await GetXmlAsync("OpStateExt", operation, fields, cancellationToken).ConfigureAwait(false);
        if (root.Name.LocalName != "OperationStateResponse") throw Invalid(operation);
        var state = Child(root, "State");
        if (!int.TryParse(Value(state, "Code"), NumberStyles.None, CultureInfo.InvariantCulture, out var code)) throw Invalid(operation);
        var info = Child(root, "Info");
        var infoValues = info?.Elements().Where(e => !e.HasElements).ToDictionary(e => e.Name.LocalName, e => e.Value) ?? [];
        var userValues = new Dictionary<string, string>();
        foreach (var field in Child(root, "UserFields")?.Elements() ?? [])
        {
            var key = (string?)field.Attribute("Name") ?? Value(field, "Name");
            var value = (string?)field.Attribute("Value") ?? Value(field, "Value");
            if (key is not null && value is not null) userValues[key] = value;
        }
        return new(code, Value(info, "OutSum"), Value(info, "OpKey"), Date(Value(state, "RequestDate"), operation),
            Date(Value(state, "StateDate"), operation), new ReadOnlyDictionary<string, string>(infoValues), new ReadOnlyDictionary<string, string>(userValues));
    }

    public async Task<IReadOnlyList<PaymentMethodGroup>> GetCurrenciesAsync(string language = "ru", CancellationToken cancellationToken = default)
    {
        if (language is not ("ru" or "en")) throw new ArgumentException("неверный язык");
        const string operation = "reference.currencies";
        var root = await GetXmlAsync("GetCurrencies", operation, new Dictionary<string, string>
        { ["MerchantLogin"] = options.MerchantLogin, ["Language"] = language }, cancellationToken).ConfigureAwait(false);
        if (root.Name.LocalName != "CurrenciesList") throw Invalid(operation);
        return (Child(root, "Groups")?.Elements().Where(e => e.Name.LocalName == "Group") ?? []).Select(g =>
            new PaymentMethodGroup((string?)g.Attribute("Code"), (string?)g.Attribute("Description"),
                (Child(g, "Items")?.Elements().Where(e => e.Name.LocalName == "Currency") ?? []).Select(c =>
                    new PaymentMethod((string?)c.Attribute("Label"), (string?)c.Attribute("Alias"), (string?)c.Attribute("Name"),
                        (string?)c.Attribute("MinValue"), (string?)c.Attribute("MaxValue"))).ToArray())).ToArray();
    }

    private async Task<XElement> GetXmlAsync(string method, string operation, Dictionary<string, string> fields, CancellationToken ct)
    {
        var body = await transport.SendAsync(operation, HttpMethod.Get,
            Wire.WithQuery(options.Endpoints.Payment("Merchant/WebService/Service.asmx/" + method), fields), null, ct).ConfigureAwait(false);
        try
        {
            using var reader = XmlReader.Create(new StringReader(body), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = options.MaxResponseBytes });
            var root = XDocument.Load(reader).Root ?? throw Invalid(operation);
            var result = Child(root, "Result");
            if (!int.TryParse(Value(result, "Code"), NumberStyles.None, CultureInfo.InvariantCulture, out var code)) throw Invalid(operation);
            if (code != 0) throw new RobokassaException(RobokassaErrorKind.Api, operation, apiCode: code);
            return root;
        }
        catch (XmlException) { throw Invalid(operation); }
    }

    private static XElement? Child(XElement? e, string name) => e?.Elements().FirstOrDefault(c => c.Name.LocalName == name);
    private static string? Value(XElement? e, string name) => Child(e, name)?.Value;
    private static DateTimeOffset? Date(string? value, string operation) => value is null ? null :
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw Invalid(operation);
    private static RobokassaException Invalid(string operation) => new(RobokassaErrorKind.InvalidResponse, operation);
}
