// модели созданы из spec/robokassa.yaml скриптом scripts/generate_models.py
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Robokassa.Models;

public sealed class SplitPaymentInvoice
{
    [JsonPropertyName("outAmount")]
    public required decimal OutAmount { get; init; }

    [JsonPropertyName("shop_params")]
    public IReadOnlyList<SplitPaymentShopParam>? ShopParams { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("incCurr")]
    public string? IncCurr { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("expirationDate")]
    public DateTimeOffset? ExpirationDate { get; init; }

    [JsonPropertyName("merchant")]
    public required SplitPaymentMerchant Merchant { get; init; }

    [JsonPropertyName("splitMerchants")]
    public required IReadOnlyList<SplitPaymentParticipant> SplitMerchants { get; init; }

}

public sealed class SplitPaymentShopParam
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

}

public sealed class SplitPaymentMerchant
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }

}

public sealed class SplitPaymentParticipant
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("InvoiceId")]
    public long? InvoiceId { get; init; }

    [JsonPropertyName("amount")]
    public required decimal Amount { get; init; }

    [JsonPropertyName("receipt")]
    public SplitPaymentReceipt? Receipt { get; init; }

}

public sealed class SplitPaymentReceipt
{
    [JsonPropertyName("sno")]
    public string? Sno { get; init; }

    [JsonPropertyName("items")]
    public required IReadOnlyList<SplitPaymentReceiptItem> Items { get; init; }

}

public sealed class SplitPaymentReceiptItem
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

public sealed class ResultUrl2Notification
{
    [JsonPropertyName("header")]
    public required ResultUrl2NotificationHeader Header { get; init; }

    [JsonPropertyName("data")]
    public required ResultUrl2NotificationData Data { get; init; }

}

public sealed class RegisterFiscalReceiptRequest
{
    [JsonPropertyName("ShopLocator")]
    public required FiscalShopLocator ShopLocator { get; init; }

    [JsonPropertyName("Receipts")]
    public required IReadOnlyList<DeferredFiscalReceipt> Receipts { get; init; }

}

public sealed class FiscalShopLocator
{
    [JsonPropertyName("MrhId")]
    public long? MrhId { get; init; }

    [JsonPropertyName("Id")]
    public string? Id { get; init; }

    [JsonPropertyName("Identifier")]
    public string? Identifier { get; init; }

}

public sealed class DeferredFiscalReceipt
{
    [JsonPropertyName("OpId")]
    public required long OpId { get; init; }

    [JsonPropertyName("Url")]
    public string? Url { get; init; }

    [JsonPropertyName("TaxScheme")]
    public string? TaxScheme { get; init; }

    [JsonPropertyName("Type")]
    public required string Type { get; init; }

    [JsonPropertyName("VatType")]
    public string? VatType { get; init; }

    [JsonPropertyName("Items")]
    public required IReadOnlyList<DeferredFiscalItem> Items { get; init; }

    [JsonPropertyName("CorrectionInfo")]
    public DeferredCorrectionInfo? CorrectionInfo { get; init; }

    [JsonPropertyName("Payments")]
    public required IReadOnlyList<DeferredFiscalPayment> Payments { get; init; }

    [JsonPropertyName("Client")]
    public DeferredFiscalClient? Client { get; init; }

}

public sealed class DeferredFiscalItem
{
    [JsonPropertyName("Name")]
    public required string Name { get; init; }

    [JsonPropertyName("Quantity")]
    public required decimal Quantity { get; init; }

    [JsonPropertyName("Sum")]
    public decimal? Sum { get; init; }

    [JsonPropertyName("Cost")]
    public decimal? Cost { get; init; }

    [JsonPropertyName("BonusSum")]
    public decimal? BonusSum { get; init; }

    [JsonPropertyName("VatType")]
    public required string VatType { get; init; }

    [JsonPropertyName("PaymentMethod")]
    public required string PaymentMethod { get; init; }

    [JsonPropertyName("PaymentObject")]
    public required string PaymentObject { get; init; }

    [JsonPropertyName("NomenclatureCode")]
    public string? NomenclatureCode { get; init; }

    [JsonPropertyName("AgentInfo")]
    public DeferredAgentInfo? AgentInfo { get; init; }

    [JsonPropertyName("SupplierInfo")]
    public DeferredSupplierInfo? SupplierInfo { get; init; }

    [JsonPropertyName("IsNonAgentItem")]
    public bool? IsNonAgentItem { get; init; }

}

public sealed class DeferredAgentInfo
{
    [JsonPropertyName("Type")]
    public string? Type { get; init; }

}

public sealed class DeferredSupplierInfo
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("Inn")]
    public string? Inn { get; init; }

    [JsonPropertyName("Phones")]
    public IReadOnlyList<string>? Phones { get; init; }

}

public sealed class DeferredCorrectionInfo
{
    [JsonPropertyName("Type")]
    public string? Type { get; init; }

    [JsonPropertyName("Description")]
    public string? Description { get; init; }

    [JsonPropertyName("CorrectedReceiptId")]
    public int? CorrectedReceiptId { get; init; }

    [JsonPropertyName("Prescription")]
    public string? Prescription { get; init; }

    [JsonPropertyName("BaseDate")]
    public string? BaseDate { get; init; }

}

public sealed class DeferredFiscalPayment
{
    [JsonPropertyName("Type")]
    public required string Type { get; init; }

    [JsonPropertyName("Sum")]
    public required decimal Sum { get; init; }

}

public sealed class DeferredFiscalClient
{
    [JsonPropertyName("Email")]
    public string? Email { get; init; }

    [JsonPropertyName("Phone")]
    public string? Phone { get; init; }

}

public sealed class FiscalReceiptError
{
    [JsonPropertyName("Text")]
    public required string Text { get; init; }

    [JsonPropertyName("Type")]
    public required string Type { get; init; }

}

public sealed class FiscalReceiptRegistrationResult
{
    [JsonPropertyName("IsSuccess")]
    public required bool IsSuccess { get; init; }

    [JsonPropertyName("OpId")]
    public required long OpId { get; init; }

    [JsonPropertyName("ReceiptId")]
    public long? ReceiptId { get; init; }

    [JsonPropertyName("ReceiptType")]
    public string? ReceiptType { get; init; }

    [JsonPropertyName("Errors")]
    public IReadOnlyList<FiscalReceiptError>? Errors { get; init; }

}

public sealed class FiscalReceiptRegisterResponse
{
    [JsonPropertyName("IsSuccess")]
    public required bool IsSuccess { get; init; }

    [JsonPropertyName("Data")]
    public FiscalReceiptRegisterResponseData? Data { get; init; }

    [JsonPropertyName("Errors")]
    public IReadOnlyList<FiscalReceiptError>? Errors { get; init; }

}

public sealed class CreateInvoiceRequest
{
    [JsonPropertyName("InvoiceType")]
    public string InvoiceType { get; init; } = "OneTime";

    [JsonPropertyName("Culture")]
    public string Culture { get; init; } = "ru";

    [JsonPropertyName("InvId")]
    public long? InvId { get; init; }

    [JsonPropertyName("OutSum")]
    public decimal? OutSum { get; init; }

    [JsonPropertyName("ExpirationDate")]
    public DateTimeOffset? ExpirationDate { get; init; }

    [JsonPropertyName("Description")]
    public string? Description { get; init; }

    [JsonPropertyName("FiscalParentOpId")]
    public long? FiscalParentOpId { get; init; }

    [JsonPropertyName("MerchantComments")]
    public string? MerchantComments { get; init; }

    [JsonPropertyName("UserFields")]
    public IReadOnlyDictionary<string, string>? UserFields { get; init; }

    [JsonPropertyName("InvoiceItems")]
    public IReadOnlyList<InvoiceItem>? InvoiceItems { get; init; }

    [JsonPropertyName("SuccessUrl2Data")]
    public RedirectData? SuccessUrl2Data { get; init; }

    [JsonPropertyName("FailUrl2Data")]
    public RedirectData? FailUrl2Data { get; init; }

    [JsonPropertyName("Aliases")]
    public IReadOnlyList<string>? Aliases { get; init; }

    [JsonPropertyName("Payments")]
    public IReadOnlyList<InvoicePayment>? Payments { get; init; }

    [JsonPropertyName("CustomUserProperty")]
    public InvoiceCustomUserProperty? CustomUserProperty { get; init; }

    [JsonPropertyName("IsWithoutFreeSale")]
    public bool? IsWithoutFreeSale { get; init; }

    [JsonPropertyName("Sno")]
    public string? Sno { get; init; }

    [JsonPropertyName("AdditionalParameters")]
    public InvoiceParameters? AdditionalParameters { get; init; }

}

public sealed class DeactivateInvoiceRequest
{
    [JsonPropertyName("EncodedId")]
    public string? EncodedId { get; init; }

    [JsonPropertyName("Id")]
    public string? Id { get; init; }

    [JsonPropertyName("InvId")]
    public long? InvId { get; init; }

}

public sealed class InvoiceFilter
{
    [JsonPropertyName("CurrentPage")]
    public required int CurrentPage { get; init; }

    [JsonPropertyName("PageSize")]
    public required int PageSize { get; init; }

    [JsonPropertyName("InvoiceStatuses")]
    public required IReadOnlyList<string> InvoiceStatuses { get; init; }

    [JsonPropertyName("Keywords")]
    public string? Keywords { get; init; }

    [JsonPropertyName("DateFrom")]
    public required DateTimeOffset DateFrom { get; init; }

    [JsonPropertyName("DateTo")]
    public required DateTimeOffset DateTo { get; init; }

    [JsonPropertyName("IsAscending")]
    public bool? IsAscending { get; init; }

    [JsonPropertyName("InvoiceTypes")]
    public required IReadOnlyList<string> InvoiceTypes { get; init; }

    [JsonPropertyName("Aliases")]
    public IReadOnlyList<string>? Aliases { get; init; }

    [JsonPropertyName("PaymentAliases")]
    public IReadOnlyList<string>? PaymentAliases { get; init; }

    [JsonPropertyName("SumFrom")]
    public decimal? SumFrom { get; init; }

    [JsonPropertyName("SumTo")]
    public decimal? SumTo { get; init; }

}

public sealed class GetInvoiceRequest
{
    [JsonPropertyName("Id")]
    public string? Id { get; init; }

    [JsonPropertyName("InvId")]
    public long? InvId { get; init; }

}

public sealed class NotifyCustomerRequest
{
    [JsonPropertyName("InvId")]
    public required long InvId { get; init; }

    [JsonPropertyName("CustomerNotificationEmail")]
    public string? CustomerNotificationEmail { get; init; }

    [JsonPropertyName("CustomerNotificationName")]
    public string? CustomerNotificationName { get; init; }

    [JsonPropertyName("CustomerNotificationPhone")]
    public string? CustomerNotificationPhone { get; init; }

}

public sealed class InvoiceItem
{
    [JsonPropertyName("Name")]
    public required string Name { get; init; }

    [JsonPropertyName("Quantity")]
    public required decimal Quantity { get; init; }

    [JsonPropertyName("Cost")]
    public required decimal Cost { get; init; }

    [JsonPropertyName("Tax")]
    public required string Tax { get; init; }

    [JsonPropertyName("PaymentMethod")]
    public string? PaymentMethod { get; init; }

    [JsonPropertyName("PaymentObject")]
    public string? PaymentObject { get; init; }

    [JsonPropertyName("NomenclatureCode")]
    public string? NomenclatureCode { get; init; }

    [JsonPropertyName("AgentInfo")]
    public InvoiceAgentInfo? AgentInfo { get; init; }

    [JsonPropertyName("SupplierInfo")]
    public InvoiceSupplierInfo? SupplierInfo { get; init; }

    [JsonPropertyName("IsNonAgentItem")]
    public bool? IsNonAgentItem { get; init; }

}

public sealed class InvoiceAgentInfo
{
    [JsonPropertyName("Type")]
    public string? Type { get; init; }

}

public sealed class InvoiceSupplierInfo
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("Inn")]
    public string? Inn { get; init; }

    [JsonPropertyName("Phones")]
    public IReadOnlyList<string>? Phones { get; init; }

}

public sealed class InvoicePayment
{
    [JsonPropertyName("Type")]
    public string? Type { get; init; }

    [JsonPropertyName("Sum")]
    public required decimal Sum { get; init; }

}

public sealed class InvoiceCustomUserProperty
{
    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("Value")]
    public string? Value { get; init; }

}

public sealed class CreateInvoiceResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("invId")]
    public required long InvId { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("isSuccess")]
    public required bool IsSuccess { get; init; }

}

public sealed class InvoiceActionResponse
{
    [JsonPropertyName("isSuccess")]
    public required bool IsSuccess { get; init; }

}

public sealed class InvoiceInformationListResponse
{
    [JsonPropertyName("invoiceInformationList")]
    public required IReadOnlyList<InvoiceInformation> InvoiceInformationList { get; init; }

    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("isSuccess")]
    public required bool IsSuccess { get; init; }

}

public sealed class GetInvoiceResponse
{
    [JsonPropertyName("invoiceInformation")]
    public required InvoiceInformation InvoiceInformation { get; init; }

    [JsonPropertyName("isSuccess")]
    public required bool IsSuccess { get; init; }

}

public sealed class InvoiceInformation
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("invId")]
    public required long InvId { get; init; }

    [JsonPropertyName("invoiceType")]
    public required string InvoiceType { get; init; }

    [JsonPropertyName("created")]
    public required DateTimeOffset Created { get; init; }

    [JsonPropertyName("modified")]
    public required DateTimeOffset Modified { get; init; }

    [JsonPropertyName("merchantLogin")]
    public string? MerchantLogin { get; init; }

    [JsonPropertyName("culture")]
    public string? Culture { get; init; }

    [JsonPropertyName("outSum")]
    public required decimal OutSum { get; init; }

    [JsonPropertyName("expirationDate")]
    public DateTimeOffset? ExpirationDate { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("fiscalParentOpId")]
    public int? FiscalParentOpId { get; init; }

    [JsonPropertyName("merchantComments")]
    public string? MerchantComments { get; init; }

    [JsonPropertyName("invoiceItems")]
    public required IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> InvoiceItems { get; init; }

    [JsonPropertyName("customerNotificationEmail")]
    public string? CustomerNotificationEmail { get; init; }

    [JsonPropertyName("isCustomerNotificationEmailSent")]
    public required bool IsCustomerNotificationEmailSent { get; init; }

    [JsonPropertyName("opId")]
    public int? OpId { get; init; }

    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; init; }

    [JsonPropertyName("invoiceStatus")]
    public required string InvoiceStatus { get; init; }

    [JsonPropertyName("invoicePaymentUrl")]
    public string? InvoicePaymentUrl { get; init; }

    [JsonPropertyName("shortInvoicePaymentUrl")]
    public string? ShortInvoicePaymentUrl { get; init; }

    [JsonPropertyName("successUrl2Data")]
    public JsonElement? SuccessUrl2Data { get; init; }

    [JsonPropertyName("failUrl2Data")]
    public JsonElement? FailUrl2Data { get; init; }

    [JsonPropertyName("aliases")]
    public required IReadOnlyList<string> Aliases { get; init; }

    [JsonPropertyName("paymentAlias")]
    public string? PaymentAlias { get; init; }

    [JsonPropertyName("paymentCardNumber")]
    public string? PaymentCardNumber { get; init; }

    [JsonPropertyName("userFields")]
    public required IReadOnlyDictionary<string, string> UserFields { get; init; }

    [JsonPropertyName("payments")]
    public required IReadOnlyList<InvoicePayment> Payments { get; init; }

    [JsonPropertyName("customUserProperty")]
    public JsonElement? CustomUserProperty { get; init; }

    [JsonPropertyName("isWithoutFreeSale")]
    public required bool IsWithoutFreeSale { get; init; }

    [JsonPropertyName("additionalParameters")]
    public IReadOnlyDictionary<string, string>? AdditionalParameters { get; init; }

}

public sealed class CreateRefundRequest
{
    [JsonPropertyName("OpKey")]
    public required string OpKey { get; init; }

    [JsonPropertyName("RefundSum")]
    public decimal? RefundSum { get; init; }

    [JsonPropertyName("InvoiceItems")]
    public IReadOnlyList<InvoiceItem>? InvoiceItems { get; init; }

}

public sealed class RefundCreateResponse
{
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

}

public sealed class RefundState
{
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("amount")]
    public required decimal Amount { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

}

public sealed class RedirectData
{
    [JsonPropertyName("Url")]
    public required string Url { get; init; }

    [JsonPropertyName("Method")]
    public required string Method { get; init; }

}

public sealed class SmsResponse
{
    [JsonPropertyName("result")]
    public required JsonElement Result { get; init; }

    [JsonPropertyName("count")]
    public int? Count { get; init; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

}

public sealed class ResultUrl2NotificationHeader
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

}

public sealed class ResultUrl2NotificationData
{
    [JsonPropertyName("shop")]
    public required string Shop { get; init; }

    [JsonPropertyName("opKey")]
    public required string OpKey { get; init; }

    [JsonPropertyName("invId")]
    public required string InvId { get; init; }

    [JsonPropertyName("paymentMethod")]
    public required string PaymentMethod { get; init; }

    [JsonPropertyName("incSum")]
    public required string IncSum { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

}

public sealed class FiscalReceiptRegisterResponseData
{
    [JsonPropertyName("ReceiptRegistrationResults")]
    public IReadOnlyList<FiscalReceiptRegistrationResult>? ReceiptRegistrationResults { get; init; }

}
