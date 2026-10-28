using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Robokassa;

internal sealed class Transport(HttpClient http, RobokassaOptions options)
{
    internal async Task<string> SendAsync(string operation, HttpMethod method, Uri uri, HttpContent? content, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Robokassa.NET", "0.1.0"));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new RobokassaException(RobokassaErrorKind.Http, operation, response.StatusCode);
            if (response.Content.Headers.ContentLength > options.MaxResponseBytes)
                throw new RobokassaException(RobokassaErrorKind.InvalidResponse, operation);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + count > options.MaxResponseBytes)
                    throw new RobokassaException(RobokassaErrorKind.InvalidResponse, operation);
                buffer.Write(chunk, 0, count);
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RobokassaException(RobokassaErrorKind.Timeout, operation);
        }
        catch (HttpRequestException)
        {
            throw new RobokassaException(RobokassaErrorKind.Transport, operation);
        }
        catch (IOException)
        {
            throw new RobokassaException(RobokassaErrorKind.Transport, operation);
        }
    }

    internal async Task<T> JsonAsync<T>(string operation, HttpMethod method, Uri uri, HttpContent? content,
        CancellationToken cancellationToken, string? successField = null)
    {
        var body = await SendAsync(operation, method, uri, content, cancellationToken).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException();
            if (successField is not null)
            {
                if (!doc.RootElement.TryGetProperty(successField, out var success) || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new JsonException();
                if (!success.GetBoolean()) throw new RobokassaException(RobokassaErrorKind.Api, operation);
            }
            return doc.RootElement.Deserialize<T>(Wire.Json) ?? throw new JsonException();
        }
        catch (JsonException) { throw new RobokassaException(RobokassaErrorKind.InvalidResponse, operation); }
    }

    internal Task<T> JwtAsync<T>(string operation, Uri uri, object payload, string key, CancellationToken cancellationToken,
        string successField, RobokassaHashAlgorithm? algorithm = null)
    {
        var jwt = RobokassaSignature.Jwt(payload, key, algorithm ?? options.HashAlgorithm);
        return JsonAsync<T>(operation, HttpMethod.Post, uri,
            new StringContent(JsonSerializer.Serialize(jwt), Encoding.UTF8, "application/json"), cancellationToken, successField);
    }
}
