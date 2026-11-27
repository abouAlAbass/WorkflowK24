using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Connections;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.ValueObjects;

namespace WorkflowEngine.Application.Services.Executors;

/// <summary>
/// Generic, SSRF-hardened HTTP/REST node executor with:
/// - Connection &amp; ISecretProvider separation (no plaintext secrets in definitions or events)
/// - Strict SSRF protection (allowed_hosts, post-DNS IP check, manual redirect revalidation)
/// - Error classification: TECHNICAL (exponential backoff + Retry-After), BUSINESS (onBusinessError.routeTo), FATAL (immediate Faulted)
/// - Typed response_mapping via simple JSONPath ($.a.b) and Idempotency-Key header
/// - InstanceEvents journaling (Http.AttemptStarted, Http.AttemptFailed, Http.Succeeded) with strict secret redaction
/// </summary>
public class HttpNodeExecutor : INodeExecutor
{
    public const int DefaultTimeoutMs = 10_000;
    public const int DefaultMaxResponseBytes = 1_048_576; // 1 MB
    public const int MaxRedirectHops = 5;

    private static readonly HashSet<string> SupportedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http",
        "httpRequest",
        "http_request"
    };

    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "X-API-Key",
        "Api-Key",
        "Cookie",
        "Set-Cookie"
    };

    private readonly IConnectionRepository _connectionRepository;
    private readonly ISecretProvider _secretProvider;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IHttpConnectorTransport _transport;
    private readonly IExecutionSecretResolver? _executionSecretResolver;

    public HttpNodeExecutor(
        IConnectionRepository connectionRepository,
        ISecretProvider secretProvider,
        ISsrfGuard ssrfGuard,
        IHttpConnectorTransport transport,
        IExecutionSecretResolver? executionSecretResolver = null)
    {
        _connectionRepository = connectionRepository ?? throw new ArgumentNullException(nameof(connectionRepository));
        _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
        _ssrfGuard = ssrfGuard ?? throw new ArgumentNullException(nameof(ssrfGuard));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _executionSecretResolver = executionSecretResolver;
    }

    public bool CanExecute(string nodeType) => SupportedNodeTypes.Contains(nodeType);

    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken ct = default)
    {
        var instance = context.Instance;
        var node = context.Node;
        var httpData = node.Data.HttpData;

        if (httpData == null || string.IsNullOrWhiteSpace(httpData.ConnectionRef))
        {
            return RecordFatalFailure(
                instance, node.Id, 1, "HTTP_CONFIG_INVALID",
                "HTTP node is missing required 'httpData.connectionRef' configuration.",
                Array.Empty<string>());
        }

        var connection = await _connectionRepository.GetByIdOrNameAsync(httpData.ConnectionRef, ct);
        if (connection == null)
        {
            return RecordFatalFailure(
                instance, node.Id, 1, "HTTP_CONNECTION_NOT_FOUND",
                $"Connection '{httpData.ConnectionRef}' was not found.",
                Array.Empty<string>());
        }

        string executionKey = $"{instance.InstanceId}:{node.Id}";
        var secretsToRedact = new List<string>();

        // 1. Resolve authentication secret if required (NEVER log it)
        (string? authHeaderName, string? authHeaderValue, string? authError) =
            await ResolveAuthHeaderAsync(connection, instance.InitiatorId, secretsToRedact, ct);

        if (authError != null)
        {
            return RecordFatalFailure(
                instance, node.Id, 1, "HTTP_SECRET_RESOLUTION_FAILED",
                authError, secretsToRedact);
        }

        // 2. Build target URI safely via template resolver (respecting active TAMAM_ENVIRONMENT override)
        string effectiveBaseUrl = _executionSecretResolver != null
            ? await _executionSecretResolver.ResolveEffectiveBaseUrlAsync(connection, ct)
            : connection.BaseUrl;
        if (!TryBuildTargetUri(effectiveBaseUrl, httpData, instance, executionKey, out Uri? targetUri, out string? uriError))
        {
            return RecordFatalFailure(
                instance, node.Id, 1, "HTTP_INVALID_URI",
                uriError ?? "Invalid target URI.", secretsToRedact);
        }

        // 3. Validate target URI against SSRF guard BEFORE any network call or retry
        var allowedHosts = connection.GetAllowedHosts();
        var ssrfResult = await _ssrfGuard.ValidateAsync(targetUri!, allowedHosts, ct);
        if (!ssrfResult.IsAllowed)
        {
            return RecordFatalFailure(
                instance, node.Id, 1, "SSRF_BLOCKED",
                ssrfResult.BlockReason ?? "Target URL blocked by SSRF guard.",
                secretsToRedact);
        }

        string methodName = string.IsNullOrWhiteSpace(httpData.Method) ? "GET" : httpData.Method.Trim().ToUpperInvariant();
        var httpMethod = new HttpMethod(methodName);

        string idempotencyKey = !string.IsNullOrWhiteSpace(httpData.IdempotencyKey)
            ? SafeTemplateResolver.ResolveString(httpData.IdempotencyKey, instance, executionKey)
            : executionKey;

        string? serializedBody = null;
        if (httpData.Body.HasValue && httpData.Body.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            var resolvedNode = SafeTemplateResolver.ResolveJsonElement(httpData.Body.Value, instance, executionKey);
            serializedBody = resolvedNode?.ToJsonString();
        }

        var retryPolicy = httpData.RetryPolicy ?? new HttpRetryPolicyPayload();
        int maxAttempts = Math.Clamp(retryPolicy.MaxAttempts, 1, 10);
        int initialDelayMs = Math.Clamp(retryPolicy.InitialDelayMs, 0, 30_000);
        double backoffMultiplier = retryPolicy.BackoffMultiplier >= 1.0 ? retryPolicy.BackoffMultiplier : 2.0;
        int maxDelayMs = Math.Clamp(retryPolicy.MaxDelayMs, 0, 60_000);
        TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Clamp(httpData.TimeoutMs ?? DefaultTimeoutMs, 100, 120_000));
        int maxResponseBytes = Math.Clamp(httpData.MaxResponseBytes ?? DefaultMaxResponseBytes, 1024, 50_000_000);

        // Pre-resolve non-secret headers (connection defaults + node overrides)
        var resolvedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in connection.GetDefaultHeaders())
        {
            resolvedHeaders[kv.Key] = SafeTemplateResolver.ResolveString(kv.Value, instance, executionKey);
        }
        if (httpData.Headers != null)
        {
            foreach (var kv in httpData.Headers)
            {
                resolvedHeaders[kv.Key] = SafeTemplateResolver.ResolveString(kv.Value, instance, executionKey);
            }
        }

        // Capture any sensitive header values for redaction too
        foreach (var kv in resolvedHeaders)
        {
            if (SensitiveHeaderNames.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            {
                secretsToRedact.Add(kv.Value);
            }
        }

        string safeUrlForEvents = SanitizeUrlForLogging(targetUri!);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var sw = Stopwatch.StartNew();

            EmitEvent(instance, "Http.AttemptStarted", node.Id, new
            {
                attempt,
                maxAttempts,
                method = methodName,
                url = safeUrlForEvents,
                connectionRef = connection.Name,
                idempotencyKey
            }, secretsToRedact);

            HttpResponseMessage? response = null;
            try
            {
                var sendOutcome = await SendWithRedirectValidationAsync(
                    targetUri!,
                    httpMethod,
                    resolvedHeaders,
                    authHeaderName,
                    authHeaderValue,
                    idempotencyKey,
                    serializedBody,
                    allowedHosts,
                    timeout,
                    ct);

                if (sendOutcome.SsrfBlockedReason != null)
                {
                    return RecordFatalFailure(
                        instance, node.Id, attempt, "SSRF_REDIRECT_BLOCKED",
                        sendOutcome.SsrfBlockedReason, secretsToRedact);
                }

                response = sendOutcome.Response!;
                int statusCode = (int)response.StatusCode;

                // Read response body with strict maxResponseBytes enforcement
                var (bodyBytes, exceededLimit) = await ReadBodyWithLimitAsync(response, maxResponseBytes, ct);
                if (exceededLimit)
                {
                    return RecordFatalFailure(
                        instance, node.Id, attempt, "HTTP_RESPONSE_TOO_LARGE",
                        $"HTTP response exceeded maximum allowed size of {maxResponseBytes} bytes.",
                        secretsToRedact);
                }

                string responseBodyText = bodyBytes.Length > 0 ? Encoding.UTF8.GetString(bodyBytes) : string.Empty;
                sw.Stop();

                // 2xx Success
                if (statusCode >= 200 && statusCode <= 299)
                {
                    var mappedVars = ApplyResponseMapping(instance, responseBodyText, httpData.ResponseMapping);

                    EmitEvent(instance, "Http.Succeeded", node.Id, new
                    {
                        attempt,
                        statusCode,
                        durationMs = sw.ElapsedMilliseconds,
                        mappedVariables = mappedVars
                    }, secretsToRedact);

                    return NodeExecutionResult.Completed();
                }

                // 5xx or 429 -> TECHNICAL (retriable)
                if (statusCode >= 500 || statusCode == 429)
                {
                    bool willRetry = attempt < maxAttempts;
                    int retryDelayMs = willRetry
                        ? ComputeRetryDelayMs(response, attempt, initialDelayMs, backoffMultiplier, maxDelayMs)
                        : 0;

                    string errMsg = RedactSecrets($"HTTP {statusCode} ({response.StatusCode}) from '{safeUrlForEvents}'.", secretsToRedact);
                    EmitEvent(instance, "Http.AttemptFailed", node.Id, new
                    {
                        attempt,
                        classification = "TECHNICAL",
                        statusCode,
                        errorCode = statusCode == 429 ? "HTTP_RATE_LIMITED" : "HTTP_SERVER_ERROR",
                        errorMessage = errMsg,
                        willRetry,
                        retryDelayMs
                    }, secretsToRedact);

                    if (willRetry)
                    {
                        if (retryDelayMs > 0)
                        {
                            await Task.Delay(retryDelayMs, ct);
                        }
                        continue;
                    }

                    return NodeExecutionResult.Faulted(
                        statusCode == 429 ? "HTTP_RATE_LIMITED" : "HTTP_SERVER_ERROR",
                        errMsg);
                }

                // 4xx (except 429) -> BUSINESS (no retry)
                if (statusCode >= 400 && statusCode <= 499)
                {
                    string businessErrMsg = RedactSecrets($"HTTP {statusCode} business error from '{safeUrlForEvents}'.", secretsToRedact);
                    EmitEvent(instance, "Http.AttemptFailed", node.Id, new
                    {
                        attempt,
                        classification = "BUSINESS",
                        statusCode,
                        errorCode = "HTTP_BUSINESS_ERROR",
                        errorMessage = businessErrMsg,
                        willRetry = false,
                        retryDelayMs = 0
                    }, secretsToRedact);

                    string? routeTo = ResolveBusinessErrorTarget(context.Graph, node, httpData.OnBusinessError);
                    if (!string.IsNullOrWhiteSpace(routeTo))
                    {
                        StoreBusinessErrorContext(instance, statusCode, responseBodyText, httpData.OnBusinessError, secretsToRedact);
                        return NodeExecutionResult.Completed(explicitNextNodeId: routeTo);
                    }

                    return NodeExecutionResult.Faulted("HTTP_BUSINESS_ERROR", businessErrMsg);
                }

                // Unexpected non-2xx/4xx/5xx status (e.g. unhandled 3xx) -> FATAL
                return RecordFatalFailure(
                    instance, node.Id, attempt, "HTTP_UNEXPECTED_STATUS",
                    $"Unexpected HTTP status code {statusCode} from '{safeUrlForEvents}'.",
                    secretsToRedact);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                sw.Stop();
                bool isTimeout = ex is TaskCanceledException;
                bool willRetry = attempt < maxAttempts;
                int retryDelayMs = willRetry
                    ? ComputeBackoffDelayMs(attempt, initialDelayMs, backoffMultiplier, maxDelayMs)
                    : 0;

                string errorCode = isTimeout ? "HTTP_TIMEOUT" : "HTTP_NETWORK_ERROR";
                string errMsg = RedactSecrets(
                    isTimeout
                        ? $"HTTP request timed out after {timeout.TotalMilliseconds:F0}ms."
                        : $"HTTP network error: {ex.Message}",
                    secretsToRedact);

                EmitEvent(instance, "Http.AttemptFailed", node.Id, new
                {
                    attempt,
                    classification = "TECHNICAL",
                    statusCode = (int?)null,
                    errorCode,
                    errorMessage = errMsg,
                    willRetry,
                    retryDelayMs
                }, secretsToRedact);

                if (willRetry)
                {
                    if (retryDelayMs > 0)
                    {
                        await Task.Delay(retryDelayMs, ct);
                    }
                    continue;
                }

                return NodeExecutionResult.Faulted(errorCode, errMsg);
            }
            finally
            {
                response?.Dispose();
            }
        }

        return NodeExecutionResult.Faulted("HTTP_RETRY_EXHAUSTED", "HTTP request failed after exhausting all retry attempts.");
    }

    private async Task<(HttpResponseMessage? Response, string? SsrfBlockedReason)> SendWithRedirectValidationAsync(
        Uri initialUri,
        HttpMethod method,
        Dictionary<string, string> headers,
        string? authHeaderName,
        string? authHeaderValue,
        string idempotencyKey,
        string? serializedBody,
        IReadOnlyList<string> allowedHosts,
        TimeSpan timeout,
        CancellationToken ct)
    {
        Uri currentUri = initialUri;
        HttpMethod currentMethod = method;
        bool sendAuth = true;

        for (int hop = 0; hop <= MaxRedirectHops; hop++)
        {
            using var request = new HttpRequestMessage(currentMethod, currentUri);

            foreach (var kv in headers)
            {
                if (SensitiveHeaderNames.Contains(kv.Key) && !sendAuth)
                {
                    continue;
                }

                request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            if (sendAuth && !string.IsNullOrWhiteSpace(authHeaderName) && !string.IsNullOrWhiteSpace(authHeaderValue))
            {
                request.Headers.Remove(authHeaderName);
                request.Headers.TryAddWithoutValidation(authHeaderName, authHeaderValue);
            }

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.Remove("Idempotency-Key");
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }

            if (serializedBody != null && currentMethod != HttpMethod.Get && currentMethod != HttpMethod.Head)
            {
                request.Content = new StringContent(serializedBody, Encoding.UTF8, "application/json");
            }

            var response = await _transport.SendAsync(request, timeout, ct);

            if (IsRedirectStatus(response.StatusCode) && response.Headers.Location != null)
            {
                Uri nextUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);

                response.Dispose();

                if (hop == MaxRedirectHops)
                {
                    return (null, $"Exceeded maximum redirect hops ({MaxRedirectHops}).");
                }

                var hopValidation = await _ssrfGuard.ValidateAsync(nextUri, allowedHosts, ct);
                if (!hopValidation.IsAllowed)
                {
                    return (null, $"Redirect target blocked by SSRF guard: {hopValidation.BlockReason}");
                }

                // Never forward credentials across different hosts
                if (!string.Equals(currentUri.Host, nextUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    sendAuth = false;
                }

                if (response.StatusCode == HttpStatusCode.SeeOther)
                {
                    currentMethod = HttpMethod.Get;
                }

                currentUri = nextUri;
                continue;
            }

            return (response, null);
        }

        return (null, "Redirect loop detected.");
    }

    private async Task<(string? HeaderName, string? HeaderValue, string? Error)> ResolveAuthHeaderAsync(
        Connection connection,
        string? actorUserId,
        List<string> secretsToRedact,
        CancellationToken ct)
    {
        string authType = connection.AuthType.Trim().ToLowerInvariant();
        bool isUserVault = string.Equals(connection.AuthMode, "user_sso_vault", StringComparison.OrdinalIgnoreCase);

        if (authType == "none" && !isUserVault)
        {
            return (null, null, null);
        }

        if (_executionSecretResolver != null)
        {
            // UserCredentialRequiredException is intentionally NOT caught here so WorkflowExecutionEngine
            // transitions the instance to Waiting and creates the personal credential task.
            var resolved = await _executionSecretResolver.ResolveAsync(connection, actorUserId, null, ct);
            if (resolved == null)
            {
                return (null, null, $"Unable to resolve credentials for connection '{connection.Name}'.");
            }

            secretsToRedact.AddRange(resolved.SecretsToRedact);
            var (headerName, headerValue) = RestServiceBroker.BuildAuthHeader(connection, resolved);
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                secretsToRedact.Add(headerValue);
            }

            return (headerName, headerValue, null);
        }

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
                string bearerVal = $"Bearer {secret}";
                secretsToRedact.Add(bearerVal);
                return ("Authorization", bearerVal, null);

            case "basic":
                string encoded = secret.Contains(':', StringComparison.Ordinal)
                    ? Convert.ToBase64String(Encoding.UTF8.GetBytes(secret))
                    : secret;
                string basicVal = $"Basic {encoded}";
                secretsToRedact.Add(encoded);
                secretsToRedact.Add(basicVal);
                return ("Authorization", basicVal, null);

            case "api_key":
                return ("X-API-Key", secret, null);

            case "oauth2_client_credentials":
                // Supports either pre-issued token string or JSON {"token":"..."}
                string token = secret;
                if (secret.TrimStart().StartsWith('{'))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(secret);
                        if (doc.RootElement.TryGetProperty("access_token", out var at) && at.ValueKind == JsonValueKind.String)
                        {
                            token = at.GetString() ?? secret;
                        }
                        else if (doc.RootElement.TryGetProperty("token", out var t) && t.ValueKind == JsonValueKind.String)
                        {
                            token = t.GetString() ?? secret;
                        }
                    }
                    catch
                    {
                        // Use raw secret fallback
                    }
                }
                string oauthVal = $"Bearer {token}";
                secretsToRedact.Add(token);
                secretsToRedact.Add(oauthVal);
                return ("Authorization", oauthVal, null);

            default:
                return (null, null, $"Unsupported AuthType '{authType}'.");
        }
    }

    private static bool TryBuildTargetUri(
        string baseUrl,
        HttpNodeDataPayload httpData,
        WorkflowInstance instance,
        string executionKey,
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

        string resolvedPath = SafeTemplateResolver.ResolveString(httpData.Path, instance, executionKey);
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

        if (httpData.QueryParams is { Count: > 0 })
        {
            var ub = new UriBuilder(combined);
            var existingQuery = ub.Query.TrimStart('?');
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(existingQuery))
            {
                parts.Add(existingQuery);
            }

            foreach (var kv in httpData.QueryParams)
            {
                string resolvedVal = SafeTemplateResolver.ResolveString(kv.Value, instance, executionKey);
                parts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(resolvedVal)}");
            }

            ub.Query = string.Join("&", parts);
            combined = ub.Uri;
        }

        uri = combined;
        return true;
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
            if (totalRead > maxBytes)
            {
                return (Array.Empty<byte>(), true);
            }

            ms.Write(buffer, 0, read);
        }

        return (ms.ToArray(), false);
    }

    private static List<string> ApplyResponseMapping(
        WorkflowInstance instance,
        string responseBodyText,
        Dictionary<string, string>? responseMapping)
    {
        var mappedKeys = new List<string>();
        if (responseMapping == null || responseMapping.Count == 0 || string.IsNullOrWhiteSpace(responseBodyText))
        {
            return mappedKeys;
        }

        JsonDocument responseDoc;
        try
        {
            responseDoc = JsonDocument.Parse(responseBodyText);
        }
        catch
        {
            return mappedKeys;
        }

        using (responseDoc)
        {
            JsonObject contextObj;
            try
            {
                contextObj = !string.IsNullOrWhiteSpace(instance.ContextVariablesJson)
                    ? JsonNode.Parse(instance.ContextVariablesJson)?.AsObject() ?? new JsonObject()
                    : new JsonObject();
            }
            catch
            {
                contextObj = new JsonObject();
            }

            foreach (var kv in responseMapping)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                if (SimpleJsonPathExtractor.TryExtract(responseDoc.RootElement, kv.Value, out var extracted))
                {
                    contextObj[kv.Key.Trim()] = extracted.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                        ? null
                        : JsonNode.Parse(extracted.GetRawText());
                    mappedKeys.Add(kv.Key.Trim());
                }
            }

            if (mappedKeys.Count > 0)
            {
                instance.UpdateVariables(contextObj.ToJsonString());
            }
        }

        return mappedKeys;
    }

    private static void StoreBusinessErrorContext(
        WorkflowInstance instance,
        int statusCode,
        string responseBodyText,
        HttpBusinessErrorPayload? businessError,
        IReadOnlyList<string> secretsToRedact)
    {
        string targetVar = !string.IsNullOrWhiteSpace(businessError?.ErrorVariable)
            ? businessError.ErrorVariable.Trim()
            : "lastHttpError";

        JsonObject contextObj;
        try
        {
            contextObj = !string.IsNullOrWhiteSpace(instance.ContextVariablesJson)
                ? JsonNode.Parse(instance.ContextVariablesJson)?.AsObject() ?? new JsonObject()
                : new JsonObject();
        }
        catch
        {
            contextObj = new JsonObject();
        }

        string safeBody = RedactSecrets(
            responseBodyText.Length > 2000 ? responseBodyText[..2000] : responseBodyText,
            secretsToRedact);

        contextObj[targetVar] = new JsonObject
        {
            ["statusCode"] = statusCode,
            ["body"] = safeBody
        };

        instance.UpdateVariables(contextObj.ToJsonString());
    }

    private static string? ResolveBusinessErrorTarget(
        ReactFlowGraph graph,
        ReactFlowNode currentNode,
        HttpBusinessErrorPayload? onBusinessError)
    {
        if (!string.IsNullOrWhiteSpace(onBusinessError?.RouteTo))
        {
            string routeTo = onBusinessError.RouteTo.Trim();
            if (graph.Nodes.Any(n => string.Equals(n.Id, routeTo, StringComparison.OrdinalIgnoreCase)))
            {
                return graph.Nodes.First(n => string.Equals(n.Id, routeTo, StringComparison.OrdinalIgnoreCase)).Id;
            }
        }

        var errorEdge = graph.Edges.FirstOrDefault(e =>
            e.Source == currentNode.Id &&
            (string.Equals(e.SourceHandle, "business-error", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.SourceHandle, "on_business_error", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(e.Label, "on_business_error", StringComparison.OrdinalIgnoreCase)));

        return errorEdge?.Target;
    }

    private static int ComputeRetryDelayMs(
        HttpResponseMessage response,
        int attempt,
        int initialDelayMs,
        double multiplier,
        int maxDelayMs)
    {
        if (response.Headers.RetryAfter != null)
        {
            if (response.Headers.RetryAfter.Delta.HasValue)
            {
                int deltaMs = (int)Math.Max(0, response.Headers.RetryAfter.Delta.Value.TotalMilliseconds);
                return Math.Min(deltaMs, maxDelayMs);
            }

            if (response.Headers.RetryAfter.Date.HasValue)
            {
                int dateMs = (int)Math.Max(0, (response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalMilliseconds);
                return Math.Min(dateMs, maxDelayMs);
            }
        }

        return ComputeBackoffDelayMs(attempt, initialDelayMs, multiplier, maxDelayMs);
    }

    private static int ComputeBackoffDelayMs(int attempt, int initialDelayMs, double multiplier, int maxDelayMs)
    {
        double raw = initialDelayMs * Math.Pow(multiplier, Math.Max(0, attempt - 1));
        return (int)Math.Min(Math.Max(0, raw), maxDelayMs);
    }

    private static bool IsRedirectStatus(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private static NodeExecutionResult RecordFatalFailure(
        WorkflowInstance instance,
        string nodeId,
        int attempt,
        string errorCode,
        string errorMessage,
        IReadOnlyList<string> secretsToRedact)
    {
        string sanitized = RedactSecrets(errorMessage, secretsToRedact);
        EmitEvent(instance, "Http.AttemptFailed", nodeId, new
        {
            attempt,
            classification = "FATAL",
            statusCode = (int?)null,
            errorCode,
            errorMessage = sanitized,
            willRetry = false,
            retryDelayMs = 0
        }, secretsToRedact);

        return NodeExecutionResult.Faulted(errorCode, sanitized);
    }

    private static void EmitEvent(
        WorkflowInstance instance,
        string eventType,
        string nodeId,
        object payload,
        IReadOnlyList<string> secretsToRedact)
    {
        string json = JsonSerializer.Serialize(payload);
        json = RedactSecrets(json, secretsToRedact);
        instance.AddEvent(eventType, nodeId, null, "system", json);
    }

    private static string SanitizeUrlForLogging(Uri uri)
    {
        // Strip userinfo if any and keep scheme + host + port + path (avoid logging secret query tokens)
        var ub = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty
        };
        return ub.Uri.ToString();
    }

    private static string RedactSecrets(string input, IReadOnlyList<string> secretsToRedact)
    {
        if (string.IsNullOrEmpty(input) || secretsToRedact.Count == 0)
        {
            return input;
        }

        string result = input;
        foreach (var secret in secretsToRedact)
        {
            if (!string.IsNullOrEmpty(secret))
            {
                result = result.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
            }
        }

        return result;
    }
}
