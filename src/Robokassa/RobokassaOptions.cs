namespace Robokassa;

public enum RobokassaHashAlgorithm { Md5, Ripemd160, Sha1, Sha256, Sha384, Sha512 }

public sealed class RobokassaOptions
{
    public required string MerchantLogin { get; init; }
    public string? Password1 { get; init; }
    public string? Password2 { get; init; }
    public string? Password3 { get; init; }
    public string? TestPassword1 { get; init; }
    public string? TestPassword2 { get; init; }
    public bool IsTest { get; init; } = true;
    public RobokassaHashAlgorithm HashAlgorithm { get; init; } = RobokassaHashAlgorithm.Md5;
    public RobokassaHashAlgorithm RefundHashAlgorithm { get; init; } = RobokassaHashAlgorithm.Sha256;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public int MaxResponseBytes { get; init; } = 4 * 1024 * 1024;
    public RobokassaEndpoints Endpoints { get; init; } = new();

    internal string PaymentPassword => Require(IsTest ? TestPassword1 : Password1,
        IsTest ? nameof(TestPassword1) : nameof(Password1));
    internal string NotificationPassword => Require(IsTest ? TestPassword2 : Password2,
        IsTest ? nameof(TestPassword2) : nameof(Password2));

    internal static string Require(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"не задан {name}");

    internal void Validate()
    {
        Require(MerchantLogin, nameof(MerchantLogin));
        if (!Enum.IsDefined(HashAlgorithm) || !Enum.IsDefined(RefundHashAlgorithm))
            throw new ArgumentOutOfRangeException(nameof(HashAlgorithm));
        if (RefundHashAlgorithm is not (RobokassaHashAlgorithm.Sha256 or RobokassaHashAlgorithm.Sha384 or RobokassaHashAlgorithm.Sha512))
            throw new ArgumentException("для возврата допустимы sha256, sha384, sha512");
        if (Timeout <= TimeSpan.Zero || Timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(Timeout));
        if (MaxResponseBytes < 1) throw new ArgumentOutOfRangeException(nameof(MaxResponseBytes));
        ArgumentNullException.ThrowIfNull(Endpoints);
    }

    internal void RequireProduction()
    {
        if (IsTest) throw new InvalidOperationException("операция недоступна в тестовом режиме");
    }
}

public sealed class RobokassaEndpoints
{
    public Uri Services { get; init; } = new("https://services.robokassa.ru/");
    public Uri Merchant { get; init; } = new("https://auth.robokassa.ru/");

    internal Uri Service(string path) => Resolve(Services, path);
    internal Uri Payment(string path) => Resolve(Merchant, path);

    private static Uri Resolve(Uri root, string path)
    {
        if (!root.IsAbsoluteUri || root.Scheme is not ("https" or "http") ||
            !string.IsNullOrEmpty(root.UserInfo) || !string.IsNullOrEmpty(root.Query) ||
            !string.IsNullOrEmpty(root.Fragment) || !root.AbsolutePath.EndsWith('/'))
            throw new ArgumentException("базовый адрес должен быть абсолютным http(s) url без query и с / в конце");
        return new Uri(root, path);
    }
}
