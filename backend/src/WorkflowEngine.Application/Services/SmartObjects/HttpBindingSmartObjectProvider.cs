using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Executors;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Services.SmartObjects;

/// <summary>
/// SmartObject provider executing external REST bindings by reusing B.1 Connection, ISecretProvider,
/// ISsrfGuard, and IHttpConnectorTransport. Normalizes HTTP errors into BUSINESS, TECHNICAL, or FATAL.
/// </summary>
public class HttpBindingSmartObjectProvider : ISmartObjectProvider
{
    public const int DefaultTimeoutMs = 10_000;
    public const int DefaultMaxResponseBytes = 1_048_576;

    private static readonly Regex InputTokenRegex = new(@"\{\{\s*inputs\.([a-zA-Z0-9_.\-]+)\s*\}\}", RegexOptions.Compiled);

    private readonly IConnectionRepository _connectionRepository;
    private readonly ISecretProvider _secretProvider;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IHttpConnectorTransport _transport;

    public HttpBindingSmartObjectProvider(
        IConnectionRepository connectionRepository,
        ISecretProvider secretProvider,
        ISsrfGuard ssrfGuard,
        IHttpConnectorTransport transport)
    {
        _connectionRepository = connectionRepository ?? throw new ArgumentNullException(nameof(connectionRepository));
        _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
        _ssrfGuard = ssrfGuard ?? throw new ArgumentNullException(nameof(ssrfGuard));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public string ProviderName => "HttpBinding";

    public bool CanHandle(string providerName) =>
        string.Equals(providerName?.Trim(), "HttpBinding", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(providerName?.Trim(), "http", StringComparison.OrdinalIgnoreCase);

    public async Task<SmartObjectProviderResult> ExecuteAsync(SmartObjectProviderContext context, CancellationToken ct = default)
    {
        var binding = context.Method.Binding;
        if (binding == null || string.IsNullOrWhiteSpace(binding.ConnectionRef))
        {
            return SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.ConfigInvalid,
                $"HttpBinding method '{context.Method.Name}' on '{context.SmartObject.ObjectRef}' is missing 'binding.connectionRef'.");
        }

        var connection = await _connectionRepository.GetByIdOrNameAsync(binding.ConnectionRef, ct);
        if (connection == null)
        {
            return SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.ConnectionNotFound,
                $"Connection '{binding.ConnectionRef}' referenced by '{context.SmartObject.ObjectRef}.{context.Method.Name}' was not found.");
        }

        (string? authHeaderName, string? authHeaderValue, string? authError) =
            await ResolveAuthHeaderAsync(connection, context.SecretsToRedact, ct);

        if (authError != null)
        {
            return SmartObjectProviderResult.FatalError(SmartObjectErrorCodes.SecretResolutionFailed, authError);
        }

        if (!TryBuildTargetUri(connection.BaseUrl, binding.Path, binding.QueryParams, context, out var targetUri, out string? uriError))
        {
            return SmartObjectProviderResult.FatalError(SmartObjectErrorCodes.ConfigInvalid, uriError ?? "Invalid target URI.");
        }

        var allowedHosts = connection.GetAllowedHosts();
        var ssrfResult = await _ssrfGuard.ValidateAsync(targetUri!, allowedHosts, ct);
        if (!ssrfResult.IsAllowed)
        {
            return SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.SsrfBlocked,
                ssrfResult.BlockReason ?? "Target URL blocked by SSRF guard.");
        }

        string httpMethodName = ResolveHttpMethod(context.Method.Kind, binding.HttpMethod);
        var httpMethod = new HttpMethod(httpMethodName);
        TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Clamp(binding.TimeoutMs ?? DefaultTimeoutMs, 100, 120_000));
        int maxResponseBytes = Math.Clamp(binding.MaxResponseBytes ?? DefaultMaxResponseBytes, 1024, 50_000_000);

        using var request = new HttpRequestMessage(httpMethod, targetUri);

        foreach (var kv in connection.GetDefaultHeaders())
        {
            request.Headers.TryAddWithoutValidation(kv.Key, ResolveStringTemplate(kv.Value, context));
        }

        if (binding.Headers != null)
        {
            foreach (var kv in binding.Headers)
            {
                request.Headers.Remove(kv.Key);
                request.Headers.TryAddWithoutValidation(kv.Key, ResolveStringTemplate(kv.Value, context));
            }
        }

        if (!string.IsNullOrWhiteSpace(authHeaderName) && !string.IsNullOrWhiteSpace(authHeaderValue))
        {
            request.Headers.Remove(authHeaderName);
            request.Headers.TryAddWithoutValidation(authHeaderName, authHeaderValue);
        }

        if (!string.IsNullOrWhiteSpace(context.IdempotencyKey))
        {
            request.Headers.Remove("Idempotency-Key");
            request.Headers.TryAddWithoutValidation("Idempotency-Key", context.IdempotencyKey);
        }

        if (httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
        {
            string bodyJson = BuildRequestBody(binding.BodyTemplate, context);
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage? response = null;
        try
        {
            response = await _transport.SendAsync(request, timeout, ct);
            int statusCode = (int)response.StatusCode;

            var (bodyBytes, exceededLimit) = await ReadBodyWithLimitAsync(response, maxResponseBytes, ct);
            if (exceededLimit)
            {
                return SmartObjectProviderResult.FatalError(
                    SmartObjectErrorCodes.ResponseTooLarge,
                    $"HTTP response exceeded maximum allowed size of {maxResponseBytes} bytes.");
            }

            string responseBody = bodyBytes.Length > 0 ? Encoding.UTF8.GetString(bodyBytes) : string.Empty;

            if (statusCode >= 200 && statusCode <= 299)
            {
                var outputs = ExtractOutputs(responseBody, binding.ResponseMapping);
                return SmartObjectProviderResult.Success(outputs);
            }

            if (statusCode >= 500 || statusCode == 429)
            {
                return SmartObjectProviderResult.TechnicalError(
                    SmartObjectErrorCodes.Unavailable,
                    $"SmartObject HttpBinding received HTTP {statusCode} ({response.StatusCode}).",
                    statusCode);
            }

            if (statusCode >= 400 && statusCode <= 499)
            {
                string code = statusCode switch
                {
                    404 => SmartObjectErrorCodes.NotFound,
                    409 => SmartObjectErrorCodes.Conflict,
                    401 or 403 => SmartObjectErrorCodes.Permission,
                    _ => SmartObjectErrorCodes.Validation
                };

                return SmartObjectProviderResult.BusinessError(
                    code,
                    $"SmartObject HttpBinding received business error HTTP {statusCode}.",
                    statusCode);
            }

            return SmartObjectProviderResult.FatalError(
                SmartObjectErrorCodes.ConfigInvalid,
                $"Unexpected HTTP status code {statusCode} from SmartObject HttpBinding.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return SmartObjectProviderResult.TechnicalError(
                SmartObjectErrorCodes.Timeout,
                $"SmartObject HttpBinding timed out after {timeout.TotalMilliseconds:F0}ms.");
        }
        catch (HttpRequestException ex)
        {
            return SmartObjectProviderResult.TechnicalError(
                SmartObjectErrorCodes.Unavailable,
                $"SmartObject HttpBinding network error: {ex.Message}");
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static string ResolveHttpMethod(string? kind, string? explicitMethod)
    {
        if (!string.IsNullOrWhiteSpace(explicitMethod))
        {
            return explicitMethod.Trim().ToUpperInvariant();
        }

        return (kind ?? "read").Trim().ToLowerInvariant() switch
        {
            "create" => "POST",
            "update" => "PUT",
            "delete" => "DELETE",
            "custom" => "POST",
            _ => "GET"
        };
    }

    private static string ResolveStringTemplate(string? template, SmartObjectProviderContext context)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        string resolved = InputTokenRegex.Replace(template, match =>
        {
            string key = match.Groups[1].Value;
            var found = context.Inputs.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (found.Key == null || found.Value == null) return string.Empty;
            if (found.Value is JsonElement el)
            {
                return el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : el.GetRawText();
            }
            return found.Value.ToString() ?? string.Empty;
        });

        return SafeTemplateResolver.ResolveString(resolved, context.Instance, context.IdempotencyKey);
    }

    private static string BuildRequestBody(JsonElement? bodyTemplate, SmartObjectProviderContext context)
    {
        if (bodyTemplate.HasValue && bodyTemplate.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            string raw = bodyTemplate.Value.GetRawText();
            string resolved = ResolveStringTemplate(raw, context);
            return resolved;
        }

        return JsonSerializer.Serialize(context.Inputs);
    }

    private static bool TryBuildTargetUri(
        string baseUrl,
        string? path,
        Dictionary<string, string>? queryParams,
        SmartObjectProviderContext context,
        out Uri? uri,
        out string? error)
    {
        uri = null;
        error = null;

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri))
        {
            error = $"Connection BaseUrl '{baseUrl}' is not a valid absolute URI.";
            return false;
        }

        string resolvedPath = ResolveStringTemplate(path ?? string.Empty, context);
        Uri combined;
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            combined = baseUri;
        }
        else if (Uri.TryCreate(resolvedPath, UriKind.Absolute, out var absoluteOverride))
        {
            combined = absoluteOverride;
        }
        else
        {
            string normalizedBase = baseUrl.TrimEnd('/');
            string normalizedPath = resolvedPath.StartsWith('/') ? resolvedPath : "/" + resolvedPath;
            if (!Uri.TryCreate(normalizedBase + normalizedPath, UriKind.Absolute, out var built))
            {
                error = $"Unable to combine BaseUrl and path '{resolvedPath}'.";
                return false;
            }
            combined = built;
        }

        if (queryParams is { Count: > 0 })
        {
            var ub = new UriBuilder(combined);
            var existingQuery = ub.Query.TrimStart('?');
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(existingQuery)) parts.Add(existingQuery);

            foreach (var kv in queryParams)
            {
                string val = ResolveStringTemplate(kv.Value, context);
                parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(val)}");
            }

            ub.Query = string.Join("&", parts);
            combined = ub.Uri;
        }

        uri = combined;
        return true;
    }

    private async Task<(string? HeaderName, string? HeaderValue, string? Error)> ResolveAuthHeaderAsync(
        Connection connection,
        List<string> secretsToRedact,
        CancellationToken ct)
    {
        string authType = connection.AuthType.Trim().ToLowerInvariant();
        if (authType == "none") return (null, null, null);

        if (string.IsNullOrWhiteSpace(connection.SecretRef))
        {
            return (null, null, $"Connection '{connection.Name}' requires a SecretRef for AuthType '{authType}'.");
        }

        string? secret = await _secretProvider.ResolveSecretAsync(connection.SecretRef, ct);
        if (string.IsNullOrWhiteSpace(secret))
        {
            return (null, null, $"Unable to resolve secret reference '{connection.SecretRef}' for connection '{connection.Name}'.");
        }

        secretsToRedact.Add(secret);

        switch (authType)
        {
            case "bearer":
                string bearer = $"Bearer {secret}";
                secretsToRedact.Add(bearer);
                return ("Authorization", bearer, null);
            case "basic":
                string encoded = secret.Contains(':', StringComparison.Ordinal)
                    ? Convert.ToBase64String(Encoding.UTF8.GetBytes(secret))
                    : secret;
                string basic = $"Basic {encoded}";
                secretsToRedact.Add(encoded);
                secretsToRedact.Add(basic);
                return ("Authorization", basic, null);
            case "api_key":
                return ("X-API-Key", secret, null);
            case "oauth2_client_credentials":
                string token = secret;
                if (secret.TrimStart().StartsWith('{'))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(secret);
                        if (doc.RootElement.TryGetProperty("access_token", out var at) && at.ValueKind == JsonValueKind.String)
                            token = at.GetString() ?? secret;
                        else if (doc.RootElement.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String)
                            token = t.GetString() ?? secret;
                    }
                    catch { }
                }
                string oauth = $"Bearer {token}";
                secretsToRedact.Add(token);
                secretsToRedact.Add(oauth);
                return ("Authorization", oauth, null);
            default:
                return (null, null, $"Unsupported AuthType '{authType}'.");
        }
    }

    private static async Task<(byte[] Bytes, bool ExceededLimit)> ReadBodyWithLimitAsync(
        HttpResponseMessage response,
        int maxBytes,
        CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength.HasValue &&
            response.Content.Headers.ContentLength.Value > maxBytes)
        {
            return (Array.Empty<byte>(), true);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        byte[] buffer = new byte[8192];
        int totalRead = 0;

        while (true)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;

            totalRead += read;
            if (totalRead > maxBytes) return (Array.Empty<byte>(), true);

            ms.Write(buffer, 0, read);
        }

        return (ms.ToArray(), false);
    }

    private static Dictionary<string, object?> ExtractOutputs(
        string responseBody,
        Dictionary<string, string>? responseMapping)
    {
        var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(responseBody)) return outputs;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);

            if (responseMapping is { Count: > 0 })
            {
                foreach (var kv in responseMapping)
                {
                    if (SimpleJsonPathExtractor.TryExtract(doc.RootElement, kv.Value, out var extracted))
                    {
                        outputs[kv.Key] = extracted.Clone();
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    outputs[prop.Name] = prop.Value.Clone();
                }
            }
        }
        catch
        {
            // Non-JSON response body
        }

        return outputs;
    }
}
