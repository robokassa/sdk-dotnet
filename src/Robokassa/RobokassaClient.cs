namespace Robokassa;

public sealed class RobokassaClient : IDisposable
{
    private readonly HttpClient http;
    private readonly bool ownsHttp;
    public InvoiceService Invoices { get; }
    public NotificationService Notifications { get; }
    public HoldingService Holding { get; }
    public RefundService Refunds { get; }
    public RecurringService Recurring { get; }
    public FiscalizationService Fiscalization { get; }
    public MessagingService Messaging { get; }
    public ReferenceService Reference { get; }
    public ClassicPaymentService ClassicPayments { get; }

    public RobokassaClient(RobokassaOptions options, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _ = options.Endpoints.Service("");
        _ = options.Endpoints.Payment("");
        http = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        ownsHttp = httpClient is null;
        var transport = new Transport(http, options);
        Invoices = new(options, transport);
        Notifications = new(options);
        Holding = new(options, transport);
        Refunds = new(options, transport);
        Recurring = new(options, transport);
        Fiscalization = new(options, transport);
        Messaging = new(options, transport);
        Reference = new(options, transport);
        ClassicPayments = new(options);
    }

    public void Dispose() { if (ownsHttp) http.Dispose(); }
}
