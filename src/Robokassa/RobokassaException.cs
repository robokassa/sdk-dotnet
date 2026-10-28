using System.Net;

namespace Robokassa;

public enum RobokassaErrorKind { Http, Transport, Timeout, Api, InvalidResponse, InvalidSignature }

public sealed class RobokassaException : Exception
{
    public RobokassaErrorKind Kind { get; }
    public string Operation { get; }
    public HttpStatusCode? StatusCode { get; }
    public int? ApiCode { get; }

    internal RobokassaException(RobokassaErrorKind kind, string operation,
        HttpStatusCode? statusCode = null, int? apiCode = null)
        : base($"ошибка robokassa: {kind}, операция {operation}")
    {
        Kind = kind;
        Operation = operation;
        StatusCode = statusCode;
        ApiCode = apiCode;
    }
}
