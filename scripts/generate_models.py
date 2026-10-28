"""генерация моделей по сохраненной спецификации, требуется pyyaml"""

import json
import re
from pathlib import Path
import yaml

root = Path(__file__).resolve().parents[1]
schemas = yaml.safe_load((root / 'spec/robokassa.yaml').read_text())['components']['schemas']
renames = {
    'InvoiceCreatePayload': 'CreateInvoiceRequest',
    'InvoiceDeactivatePayload': 'DeactivateInvoiceRequest',
    'InvoiceInformationFilter': 'InvoiceFilter',
    'InvoiceInformationPayload': 'GetInvoiceRequest',
    'InvoiceNotificationPayload': 'NotifyCustomerRequest',
    'InvoiceCreateSuccess': 'CreateInvoiceResponse',
    'InvoiceActionSuccess': 'InvoiceActionResponse',
    'InvoiceInformationSuccess': 'GetInvoiceResponse',
    'RefundPayload': 'CreateRefundRequest',
    'FiscalReceiptRegisterPayload': 'RegisterFiscalReceiptRequest',
}
selected = [k for k in schemas if k.startswith(('Invoice', 'Fiscal', 'Deferred', 'SplitPayment', 'Refund', 'ResultUrl2'))]
selected = [k for k in selected if k not in ('InvoiceError', 'SplitPaymentFormRequest')]
selected += ['RedirectData', 'SmsResponse']
pending = {renames.get(k, k): schemas[k] for k in selected if schemas[k].get('type') == 'object'}

def pascal(name):
    return ''.join(part[:1].upper() + part[1:] for part in name.split('_'))

def kind(schema, nested):
    if '$ref' in schema:
        key = schema['$ref'].split('/')[-1]
        return renames.get(key, key) if schemas[key].get('type') == 'object' else 'string'
    value = schema.get('type')
    if isinstance(value, list):
        value = next(v for v in value if v != 'null')
    if value == 'array':
        return 'IReadOnlyList<' + kind(schema['items'], nested + 'Item') + '>'
    if value == 'object':
        if 'additionalProperties' in schema:
            additional = schema['additionalProperties']
            return 'IReadOnlyDictionary<string, ' + (kind(additional, nested + 'Value') if isinstance(additional, dict) else 'JsonElement') + '>'
        if schema.get('properties'):
            pending[nested] = schema
            return nested
        return 'JsonElement'
    if value == 'integer':
        return 'long' if schema.get('format') == 'int64' else 'int'
    if value == 'number':
        return 'decimal'
    if value == 'boolean':
        return 'bool'
    if value == 'string':
        return 'DateTimeOffset' if schema.get('format') == 'date-time' else 'string'
    return 'JsonElement'

lines = ['// модели созданы из spec/robokassa.yaml скриптом scripts/generate_models.py',
         'using System.Text.Json;', 'using System.Text.Json.Serialization;', '', 'namespace Robokassa.Models;', '']
done = set()
while set(pending) - done:
    name = next(n for n in pending if n not in done)
    schema = pending[name]
    done.add(name)
    lines += ['public sealed class ' + name, '{']
    for key, value in schema.get('properties', {}).items():
        if key == 'MerchantLogin' and name in renames.values():
            continue
        type_name = kind(value, name + pascal(key))
        if name == 'CreateInvoiceRequest' and key == 'AdditionalParameters':
            type_name = 'InvoiceParameters'
        required = key in schema.get('required', [])
        default = None
        if name == 'CreateInvoiceRequest' and key in ('InvoiceType', 'Culture'):
            required = False
            default = '"OneTime"' if key == 'InvoiceType' else '"ru"'
        nullable = not required and default is None or isinstance(value.get('type'), list) and 'null' in value['type']
        lines += ['    [JsonPropertyName(' + json.dumps(key) + ')]',
                  '    public ' + ('required ' if required else '') + type_name + ('?' if nullable else '') + ' ' + pascal(key) + ' { get; init; }' + (' = ' + default + ';' if default else ''), '']
    lines += ['}', '']
(root / 'src/Robokassa/Models/ApiModels.cs').write_text('\n'.join(lines))
