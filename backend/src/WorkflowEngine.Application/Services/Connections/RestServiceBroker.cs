using System.Diagnostics;
using System.Text;
using System.Text.Json;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Services.Connections;

/// <summary>
/// Service Broker for REST/HTTP connections.
/// Delegates exclusively to the existing ISsrfGuard and IHttpConnectorTransport primitives,
/// preserving 100% backward compatibility and SSRF hardening.
/// </summary>
public class RestServiceBroker : IServiceBroker
{
    public const int TestTimeoutSeconds = 5;
    public const int DefaultInvokeTimeoutSeconds = 15;

    private readonly ISsrfGuard _ssrfGuard;
    private readonly IHttpConnectorTransport _transport;

    public RestServiceBroker(ISsrfGuard ssrfGuard, IHttpConnectorTransport transport)
    {
        _ssrfGuard = ssrfGuard ?? throw new ArgumentNullException(nameof(ssrfGuard));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public string ServiceType => "rest";

    public Task<IReadOnlyList<DiscoveredEntityDto>> DiscoverEntitiesAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<DiscoveredEntityDto>>(Array.Empty<DiscoveredEntityDto>());
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var secretsToRedact = secret?.SecretsToRedact ?? Array.Empty<string>();
        string effectiveBaseUrl = !string.IsNullOrWhiteSpace(secret?.EffectiveBaseUrl) ? secret.EffectiveBaseUrl! : connection.BaseUrl;

        if (!Uri.TryCreate(effectiveBaseUrl, UriKind.Absolute, out var targetUri) ||
            (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, "Invalid REST BaseUrl.");
        }

        var ssrfResult = await _ssrfGuard.ValidateAsync(targetUri, connection.GetAllowedHosts(), ct);
        if (!ssrfResult.IsAllowed)
        {
            sw.Stop();
            return new ConnectionTestResult(
                false,
                sw.ElapsedMilliseconds,
                ErrorSanitizer.Sanitize(ssrfResult.BlockReason ?? "Target URL blocked by SSRF guard.", secretsToRedact));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
            ApplyHeaders(request, connection, secret, null, null);

            using var response = await _transport.SendAsync(request, TimeSpan.FromSeconds(TestTimeoutSeconds), ct);
            sw.Stop();

            int status = (int)response.StatusCode;
            if (status >= 200 && status <= 399)
            {
                return new ConnectionTestResult(true, sw.ElapsedMilliseconds);
            }

            return new ConnectionTestResult(
                false,
                sw.ElapsedMilliseconds,
                $"Remote endpoint returned HTTP {status} ({response.StatusCode}).");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, $"Connection test timed out after {TestTimeoutSeconds}s.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult(
                false,
                sw.ElapsedMilliseconds,
                ErrorSanitizer.Sanitize(ex.Message, secretsToRedact));
        }
    }

    public async Task<ServiceBrokerInvocationResult> InvokeAsync(
        ServiceBrokerInvocationRequest request,
        CancellationToken ct = default)
    {
        var connection = request.Connection;
        var secretsToRedact = request.Secret?.SecretsToRedact ?? Array.Empty<string>();
        string effectiveBaseUrl = !string.IsNullOrWhiteSpace(request.Secret?.EffectiveBaseUrl)
            ? request.Secret.EffectiveBaseUrl!
            : connection.BaseUrl;

        if (!TryBuildTargetUri(effectiveBaseUrl, request.Path, out var targetUri, out string? uriError))
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "REST_INVALID_URI", ErrorMessage: uriError);
        }

        var ssrfResult = await _ssrfGuard.ValidateAsync(targetUri!, connection.GetAllowedHosts(), ct);
        if (!ssrfResult.IsAllowed)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SSRF_BLOCKED",
                ErrorMessage: ErrorSanitizer.Sanitize(ssrfResult.BlockReason, secretsToRedact));
        }

        string methodStr = !string.IsNullOrWhiteSpace(request.HttpMethod)
            ? request.HttpMethod.Trim().ToUpperInvariant()
            : request.Operation.Trim().ToLowerInvariant() switch
            {
                "create" or "post" => "POST",
                "update" or "put" => "PUT",
                "delete" => "DELETE",
                _ => "GET"
            };

        var httpMethod = new HttpMethod(methodStr);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds ?? DefaultInvokeTimeoutSeconds, 1, 120));

        try
        {
            using var httpReq = new HttpRequestMessage(httpMethod, targetUri);
            ApplyHeaders(httpReq, connection, request.Secret, request.Headers, request.IdempotencyKey);

            if (!string.IsNullOrWhiteSpace(request.BodyJson) && httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
            {
                httpReq.Content = new StringContent(request.BodyJson, Encoding.UTF8, "application/json");
            }

            using var response = await _transport.SendAsync(httpReq, timeout, ct);
            int statusCode = (int)response.StatusCode;
            string bodyText = await response.Content.ReadAsStringAsync(ct);

            object? parsedData = null;
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                try
                {
                    parsedData = JsonSerializer.Deserialize<JsonElement>(bodyText);
                }
                catch
                {
                    parsedData = bodyText;
                }
            }

            if (statusCode >= 200 && statusCode <= 299)
            {
                return new ServiceBrokerInvocationResult(true, StatusCode: statusCode, Data: parsedData);
            }

            return new ServiceBrokerInvocationResult(
                false,
                StatusCode: statusCode,
                Data: parsedData,
                ErrorCode: statusCode >= 500 ? "REST_SERVER_ERROR" : "REST_CLIENT_ERROR",
                ErrorMessage: $"REST broker received HTTP {statusCode} ({response.StatusCode}).");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "REST_TIMEOUT", ErrorMessage: $"REST request timed out after {timeout.TotalSeconds:F0}s.");
        }
        catch (Exception ex)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "REST_NETWORK_ERROR",
                ErrorMessage: ErrorSanitizer.Sanitize(ex.Message, secretsToRedact));
        }
    }

    public static (string? HeaderName, string? HeaderValue) BuildAuthHeader(
        Connection connection,
        ResolvedExecutionSecret? secret)
    {
        if (secret == null || string.IsNullOrWhiteSpace(secret.RawValue))
        {
            return (null, null);
        }

        if (string.Equals(connection.AuthMode, "on_behalf_of", StringComparison.OrdinalIgnoreCase))
        {
            string oboToken = secret.GetField("access_token", "token") ?? secret.RawValue;
            return !string.IsNullOrWhiteSpace(oboToken)
                ? ("Authorization", $"Bearer {oboToken}")
                : (null, null);
        }

        string authType = connection.AuthType.Trim().ToLowerInvariant();
        if (authType == "none" && !string.Equals(connection.AuthMode, "user_sso_vault", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        switch (authType)
        {
            case "bearer":
                string bearerToken = secret.GetField("token", "access_token", "bearer") ?? secret.RawValue;
                return ("Authorization", $"Bearer {bearerToken}");

            case "basic":
                string? user = secret.GetField("username", "user");
                string? pass = secret.GetField("password", "pwd");
                string rawBasic = !string.IsNullOrWhiteSpace(user) && pass != null
                    ? $"{user}:{pass}"
                    : secret.RawValue;
                string encoded = rawBasic.Contains(':', StringComparison.Ordinal)
                    ? Convert.ToBase64String(Encoding.UTF8.GetBytes(rawBasic))
                    : rawBasic;
                return ("Authorization", $"Basic {encoded}");

            case "api_key":
                string apiKey = secret.GetField("apiKey", "api_key", "key") ?? secret.RawValue;
                string headerName = secret.GetField("headerName") ?? "X-API-Key";
                return (headerName, apiKey);

            case "oauth2_client_credentials":
                string token = secret.GetField("access_token", "token") ?? secret.RawValue;
                return ("Authorization", $"Bearer {token}");

            default:
                // For user_sso_vault with AuthType = none, default to Bearer if token present, else Basic if username/password
                string? vaultToken = secret.GetField("token", "access_token");
                if (!string.IsNullOrWhiteSpace(vaultToken))
                {
                    return ("Authorization", $"Bearer {vaultToken}");
                }

                string? vaultUser = secret.GetField("username", "user");
                string? vaultPass = secret.GetField("password", "pwd");
                if (!string.IsNullOrWhiteSpace(vaultUser) && vaultPass != null)
                {
                    string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{vaultUser}:{vaultPass}"));
                    return ("Authorization", $"Basic {b64}");
                }

                return (null, null);
        }
    }

    private static void ApplyHeaders(
        HttpRequestMessage request,
        Connection connection,
        ResolvedExecutionSecret? secret,
        IReadOnlyDictionary<string, string>? extraHeaders,
        string? idempotencyKey)
    {
        foreach (var kv in connection.GetDefaultHeaders())
        {
            request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }

        if (extraHeaders != null)
        {
            foreach (var kv in extraHeaders)
            {
                request.Headers.Remove(kv.Key);
                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        var (authName, authVal) = BuildAuthHeader(connection, secret);
        if (!string.IsNullOrWhiteSpace(authName) && !string.IsNullOrWhiteSpace(authVal))
        {
            request.Headers.Remove(authName);
            request.Headers.TryAddWithoutValidation(authName, authVal);
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.Remove("Idempotency-Key");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }
    }

    private static bool TryBuildTargetUri(string baseUrl, string? path, out Uri? uri, out string? error)
    {
        uri = null;
        error = null;

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            error = "Connection BaseUrl is not a valid absolute URI.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            uri = baseUri;
            return true;
        }

        if (Uri.TryCreate(path.Trim(), UriKind.Absolute, out var abs))
        {
            uri = abs;
            return true;
        }

        string combined = $"{baseUrl.TrimEnd('/')}/{(path.Trim().TrimStart('/'))}";
        if (!Uri.TryCreate(combined, UriKind.Absolute, out var built))
        {
            error = $"Unable to combine BaseUrl with path '{path}'.";
            return false;
        }

        uri = built;
        return true;
    }
}
