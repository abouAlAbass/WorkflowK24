using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Connections;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Application.Services.SmartObjects;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Services.Brokers;

/// <summary>
/// SharePoint / Microsoft Graph Service Broker targeting /sites/{siteId}/lists.
/// Enforces:
/// - Read-only by default (AllowWrite = false blocks Create/Update/Delete on /items with 403 WRITE_NOT_ALLOWED_ON_CONNECTION).
/// - Strict SSRF validation via ISsrfGuard on both BaseUrl and OAuth2 tokenEndpoint, with AllowAutoRedirect = false.
/// - Polly resilience pipeline (5xx + 429 Retry-After + circuit breaker) on the named Microsoft Graph HttpClient.
/// - Mandatory $top cap (&lt;= connection.MaxRows, hard cap 1000) and 1 MiB response size ceiling.
/// - Supports 'static' and 'oauth2_client_credentials' (with IMemoryCache token caching).
/// </summary>
public class SharePointServiceBroker : IServiceBroker
{
    public const string GraphHttpClientName = "SharePointGraph";
    public const string TokenHttpClientName = "SharePointEntraToken";

    public const int TestTimeoutSeconds = 5;
    public const int DefaultInvokeTimeoutSeconds = 30;
    public const int HardMaxRows = 1000;
    public const int MaxResponseBytes = 1024 * 1024; // 1 MiB

    private static readonly HashSet<string> WriteOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "create",
        "update",
        "delete",
        "post",
        "put",
        "patch"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IMemoryCache _memoryCache;

    public SharePointServiceBroker(
        IHttpClientFactory httpClientFactory,
        ISsrfGuard ssrfGuard,
        IMemoryCache memoryCache)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _ssrfGuard = ssrfGuard ?? throw new ArgumentNullException(nameof(ssrfGuard));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    public string ServiceType => "sharepoint";

    public async Task<ConnectionTestResult> TestConnectionAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var secretsToRedact = secret?.SecretsToRedact ?? Array.Empty<string>();
        string effectiveBaseUrl = !string.IsNullOrWhiteSpace(secret?.EffectiveBaseUrl) ? secret.EffectiveBaseUrl! : connection.BaseUrl;

        if (!TryBuildTargetUri(effectiveBaseUrl, "lists?$top=1", out var targetUri, out var uriError))
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, uriError);
        }

        var ssrfResult = await _ssrfGuard.ValidateAsync(targetUri!, connection.GetAllowedHosts(), ct);
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
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(TestTimeoutSeconds));

            var tokenResult = await ResolveAccessTokenAsync(connection, secret, timeoutCts.Token);
            if (!tokenResult.Success)
            {
                sw.Stop();
                return new ConnectionTestResult(
                    false,
                    sw.ElapsedMilliseconds,
                    ErrorSanitizer.Sanitize(tokenResult.ErrorMessage, secretsToRedact));
            }

            var client = _httpClientFactory.CreateClient(GraphHttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Get, targetUri);
            ApplyHeaders(req, connection, tokenResult.AccessToken, null);

            using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            sw.Stop();

            int status = (int)response.StatusCode;
            if (IsRedirectStatusCode(status))
            {
                return new ConnectionTestResult(
                    false,
                    sw.ElapsedMilliseconds,
                    $"SharePoint Graph endpoint attempted an HTTP {status} redirect, which is blocked by SSRF policy.");
            }

            if (status >= 200 && status <= 299)
            {
                return new ConnectionTestResult(true, sw.ElapsedMilliseconds);
            }

            return new ConnectionTestResult(
                false,
                sw.ElapsedMilliseconds,
                $"SharePoint Graph endpoint returned HTTP {status} ({response.StatusCode}).");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, $"SharePoint connection test timed out after {TestTimeoutSeconds}s.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ConnectionTestResult(false, sw.ElapsedMilliseconds, ErrorSanitizer.Sanitize(ex.Message, secretsToRedact));
        }
    }

    public async Task<IReadOnlyList<DiscoveredEntityDto>> DiscoverEntitiesAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct = default)
    {
        var secretsToRedact = secret?.SecretsToRedact ?? Array.Empty<string>();
        string effectiveBaseUrl = !string.IsNullOrWhiteSpace(secret?.EffectiveBaseUrl) ? secret.EffectiveBaseUrl! : connection.BaseUrl;
        if (!TryBuildTargetUri(effectiveBaseUrl, "lists", out var listsUri, out var uriError))
        {
            throw new InvalidOperationException(uriError ?? "Invalid SharePoint BaseUrl.");
        }

        var ssrfResult = await _ssrfGuard.ValidateAsync(listsUri!, connection.GetAllowedHosts(), ct);
        if (!ssrfResult.IsAllowed)
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(ssrfResult.BlockReason ?? "Target URL blocked by SSRF guard.", secretsToRedact));
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(DefaultInvokeTimeoutSeconds));

        var tokenResult = await ResolveAccessTokenAsync(connection, secret, timeoutCts.Token);
        if (!tokenResult.Success)
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(tokenResult.ErrorMessage ?? "SharePoint authentication failed.", secretsToRedact));
        }

        var client = _httpClientFactory.CreateClient(GraphHttpClientName);
        var listsJson = await SendGraphGetAsync(client, connection, listsUri!, tokenResult.AccessToken, timeoutCts.Token);

        var entities = new List<DiscoveredEntityDto>();
        if (listsJson.ValueKind != JsonValueKind.Object ||
            !listsJson.TryGetProperty("value", out var listsArray) ||
            listsArray.ValueKind != JsonValueKind.Array)
        {
            return entities;
        }

        foreach (var listEl in listsArray.EnumerateArray())
        {
            if (listEl.ValueKind != JsonValueKind.Object) continue;

            string listId = listEl.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            string listName = listEl.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? listId : listId;
            string displayName = listEl.TryGetProperty("displayName", out var dispEl) && !string.IsNullOrWhiteSpace(dispEl.GetString())
                ? dispEl.GetString()!
                : SmartObjectTypeMapper.FormatDisplayName(listName);

            if (string.IsNullOrWhiteSpace(listId) && string.IsNullOrWhiteSpace(listName)) continue;

            string listKey = !string.IsNullOrWhiteSpace(listId) ? listId : listName;
            if (!TryBuildTargetUri(effectiveBaseUrl, $"lists/{Uri.EscapeDataString(listKey)}/columns", out var colsUri, out _))
            {
                continue;
            }

            var colsSsrf = await _ssrfGuard.ValidateAsync(colsUri!, connection.GetAllowedHosts(), timeoutCts.Token);
            if (!colsSsrf.IsAllowed) continue;

            var colsJson = await SendGraphGetAsync(client, connection, colsUri!, tokenResult.AccessToken, timeoutCts.Token);
            var fields = new List<DiscoveredFieldDto>();

            if (colsJson.ValueKind == JsonValueKind.Object &&
                colsJson.TryGetProperty("value", out var colsArray) &&
                colsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var colEl in colsArray.EnumerateArray())
                {
                    if (colEl.ValueKind != JsonValueKind.Object) continue;
                    string colName = colEl.TryGetProperty("name", out var cn) ? cn.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(colName)) continue;

                    string sourceType = DetectSharePointColumnType(colEl);
                    bool required = colEl.TryGetProperty("required", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;
                    bool isPk = string.Equals(colName, "id", StringComparison.OrdinalIgnoreCase)
                        || (colEl.TryGetProperty("isPrimaryKey", out var pkEl) && pkEl.ValueKind == JsonValueKind.True);

                    fields.Add(new DiscoveredFieldDto(
                        Name: colName,
                        SourceType: sourceType,
                        MappedSmartObjectType: SmartObjectTypeMapper.MapToSmartObjectType(sourceType),
                        IsNullable: !required,
                        IsPrimaryKey: isPk,
                        SuggestedMasked: SmartObjectTypeMapper.IsSuggestedMasked(colName)));
                }
            }

            string? detectedPk = fields.FirstOrDefault(f => f.IsPrimaryKey)?.Name;
            entities.Add(new DiscoveredEntityDto(
                EntityName: listName,
                DisplayName: displayName,
                DetectedPrimaryKey: detectedPk,
                Fields: fields,
                SchemaName: listId,
                EntityType: "SharePointList"));
        }

        return entities;
    }

    private static async Task<JsonElement> SendGraphGetAsync(
        HttpClient client,
        Connection connection,
        Uri targetUri,
        string? accessToken,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, targetUri);
        ApplyHeaders(req, connection, accessToken, null);
        using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        int status = (int)response.StatusCode;
        if (IsRedirectStatusCode(status))
        {
            throw new InvalidOperationException($"SharePoint Graph endpoint attempted an HTTP {status} redirect, which is blocked by SSRF policy.");
        }

        var (body, sizeExceeded) = await ReadBoundedResponseAsync(response.Content, MaxResponseBytes, ct);
        if (sizeExceeded)
        {
            throw new InvalidOperationException($"SharePoint Graph response exceeded the maximum allowed size of {MaxResponseBytes} bytes.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"SharePoint Graph returned HTTP {status} ({response.StatusCode}).");
        }

        return string.IsNullOrWhiteSpace(body)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static string DetectSharePointColumnType(JsonElement colEl)
    {
        if (colEl.TryGetProperty("number", out _)) return "Number";
        if (colEl.TryGetProperty("currency", out _)) return "Currency";
        if (colEl.TryGetProperty("boolean", out _)) return "YesNo";
        if (colEl.TryGetProperty("dateTime", out _)) return "DateTime";
        if (colEl.TryGetProperty("choice", out _)) return "Choice";
        if (colEl.TryGetProperty("personOrGroup", out _)) return "Person";
        if (colEl.TryGetProperty("text", out _)) return "Text";
        if (colEl.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            return typeEl.GetString() ?? "Text";
        }

        return "Text";
    }

    public async Task<ServiceBrokerInvocationResult> InvokeAsync(
        ServiceBrokerInvocationRequest request,
        CancellationToken ct = default)
    {
        var connection = request.Connection;
        var secretsToRedact = request.Secret?.SecretsToRedact ?? Array.Empty<string>();
        string operation = request.Operation?.Trim().ToLowerInvariant() ?? "list";

        string methodStr = !string.IsNullOrWhiteSpace(request.HttpMethod)
            ? request.HttpMethod.Trim().ToUpperInvariant()
            : operation switch
            {
                "create" => "POST",
                "update" => "PATCH",
                "delete" => "DELETE",
                _ => "GET"
            };

        bool isWrite = WriteOperations.Contains(operation) ||
                       methodStr is "POST" or "PUT" or "PATCH" or "DELETE";

        // 1. Strict Read-Only Guard
        if (isWrite && !connection.AllowWrite)
        {
            throw new ConnectionWriteNotAllowedException(
                connection.Id,
                request.Operation ?? methodStr,
                $"Write operation '{request.Operation ?? methodStr}' is forbidden on SharePoint connection '{connection.Name}' (AllowWrite = false).");
        }

        int effectiveMaxRows = Math.Clamp(connection.MaxRows, 1, HardMaxRows);
        var parameters = request.Parameters ?? new Dictionary<string, object?>();

        string effectiveBaseUrl = !string.IsNullOrWhiteSpace(request.Secret?.EffectiveBaseUrl)
            ? request.Secret.EffectiveBaseUrl!
            : connection.BaseUrl;
        string relativePath = BuildGraphPath(operation, request.Path, request.TableName, parameters, effectiveMaxRows);
        if (!TryBuildTargetUri(effectiveBaseUrl, relativePath, out var targetUri, out var uriError))
        {
            return new ServiceBrokerInvocationResult(false, ErrorCode: "SHAREPOINT_INVALID_URI", ErrorMessage: uriError);
        }

        // 2. Strict SSRF validation (AllowedHosts + post-DNS private/link-local IP blocking)
        var ssrfResult = await _ssrfGuard.ValidateAsync(targetUri!, connection.GetAllowedHosts(), ct);
        if (!ssrfResult.IsAllowed)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SSRF_BLOCKED",
                ErrorMessage: ErrorSanitizer.Sanitize(ssrfResult.BlockReason, secretsToRedact));
        }

        int timeoutSec = Math.Clamp(request.TimeoutSeconds ?? DefaultInvokeTimeoutSeconds, 1, 120);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        try
        {
            // 3. Resolve token (static Bearer or cached Entra ID oauth2_client_credentials)
            var tokenResult = await ResolveAccessTokenAsync(connection, request.Secret, timeoutCts.Token);
            if (!tokenResult.Success)
            {
                return new ServiceBrokerInvocationResult(
                    false,
                    ErrorCode: tokenResult.ErrorCode ?? "SHAREPOINT_AUTH_ERROR",
                    ErrorMessage: ErrorSanitizer.Sanitize(tokenResult.ErrorMessage, secretsToRedact));
            }

            var httpMethod = new HttpMethod(methodStr);
            var client = _httpClientFactory.CreateClient(GraphHttpClientName);

            using var httpReq = new HttpRequestMessage(httpMethod, targetUri);
            ApplyHeaders(httpReq, connection, tokenResult.AccessToken, request.Headers);

            if (!string.IsNullOrWhiteSpace(request.BodyJson) && httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
            {
                httpReq.Content = new StringContent(request.BodyJson, Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            int statusCode = (int)response.StatusCode;

            // 4. Reject redirects (AllowAutoRedirect = false)
            if (IsRedirectStatusCode(statusCode))
            {
                return new ServiceBrokerInvocationResult(
                    false,
                    StatusCode: statusCode,
                    ErrorCode: "SSRF_REDIRECT_BLOCKED",
                    ErrorMessage: $"SharePoint Graph request received HTTP {statusCode} redirect, which is blocked by SSRF policy.");
            }

            // 5. Enforce 1 MiB response ceiling
            var (bodyText, sizeExceeded) = await ReadBoundedResponseAsync(response.Content, MaxResponseBytes, timeoutCts.Token);
            if (sizeExceeded)
            {
                return new ServiceBrokerInvocationResult(
                    false,
                    StatusCode: statusCode,
                    ErrorCode: "SHAREPOINT_RESPONSE_TOO_LARGE",
                    ErrorMessage: $"SharePoint Graph response exceeded the maximum allowed size of {MaxResponseBytes} bytes (1 MiB).");
            }

            object? parsedData = null;
            List<Dictionary<string, object?>>? rows = null;

            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                try
                {
                    var doc = JsonSerializer.Deserialize<JsonElement>(bodyText);
                    parsedData = doc;
                    rows = ExtractGraphRows(doc, effectiveMaxRows);
                }
                catch
                {
                    parsedData = bodyText;
                }
            }

            if (statusCode >= 200 && statusCode <= 299)
            {
                return new ServiceBrokerInvocationResult(
                    true,
                    StatusCode: statusCode,
                    Data: parsedData,
                    Rows: rows,
                    AffectedRows: rows?.Count);
            }

            return new ServiceBrokerInvocationResult(
                false,
                StatusCode: statusCode,
                Data: parsedData,
                ErrorCode: statusCode == 429 ? "SHAREPOINT_THROTTLED" : statusCode >= 500 ? "SHAREPOINT_SERVER_ERROR" : "SHAREPOINT_CLIENT_ERROR",
                ErrorMessage: $"SharePoint Graph returned HTTP {statusCode} ({response.StatusCode}).");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SHAREPOINT_TIMEOUT",
                ErrorMessage: $"SharePoint Graph request timed out after {timeoutSec}s.");
        }
        catch (Exception ex)
        {
            return new ServiceBrokerInvocationResult(
                false,
                ErrorCode: "SHAREPOINT_NETWORK_ERROR",
                ErrorMessage: ErrorSanitizer.Sanitize(ex.Message, secretsToRedact));
        }
    }

    private sealed record TokenResolutionResult(bool Success, string? AccessToken = null, string? ErrorCode = null, string? ErrorMessage = null);

    private async Task<TokenResolutionResult> ResolveAccessTokenAsync(
        Connection connection,
        ResolvedExecutionSecret? secret,
        CancellationToken ct)
    {
        // OAuth2 On-Behalf-Of (OBO): ExecutionSecretResolver already exchanged the user's external IdP token
        if (string.Equals(connection.AuthMode, "on_behalf_of", StringComparison.OrdinalIgnoreCase))
        {
            string? oboToken = secret?.GetField("access_token", "token") ?? secret?.RawValue;
            if (string.IsNullOrWhiteSpace(oboToken))
            {
                return new TokenResolutionResult(
                    false,
                    ErrorCode: ExternalReauthRequiredException.ErrorCode,
                    ErrorMessage: "OAuth2 On-Behalf-Of access token could not be resolved for the current user.");
            }

            return new TokenResolutionResult(true, oboToken);
        }

        bool isOAuth2 = string.Equals(connection.AuthMode, "oauth2_client_credentials", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.AuthType, "oauth2_client_credentials", StringComparison.OrdinalIgnoreCase);

        if (!isOAuth2)
        {
            string? staticToken = secret?.GetField("access_token", "token", "bearer") ?? secret?.RawValue;
            return new TokenResolutionResult(true, staticToken);
        }

        if (secret == null)
        {
            return new TokenResolutionResult(false, ErrorCode: "SHAREPOINT_CREDENTIALS_MISSING", ErrorMessage: "OAuth2 client credentials are required for this SharePoint connection.");
        }

        // Allow direct pre-supplied token if no clientId/clientSecret provided
        string? clientId = secret.GetField("clientId", "client_id");
        string? clientSecret = secret.GetField("clientSecret", "client_secret");
        string? tenantId = secret.GetField("tenantId", "tenant_id");
        string? explicitTokenEndpoint = secret.GetField("tokenEndpoint", "token_endpoint");
        string scope = secret.GetField("scope") ?? "https://graph.microsoft.com/.default";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            string? directToken = secret.GetField("access_token", "token");
            if (!string.IsNullOrWhiteSpace(directToken))
            {
                return new TokenResolutionResult(true, directToken);
            }

            return new TokenResolutionResult(
                false,
                ErrorCode: "SHAREPOINT_OAUTH2_CONFIG_INVALID",
                ErrorMessage: "OAuth2 client credentials require 'clientId' and 'clientSecret' (and 'tenantId' or 'tokenEndpoint').");
        }

        string tokenEndpointUrl = !string.IsNullOrWhiteSpace(explicitTokenEndpoint)
            ? explicitTokenEndpoint.Trim()
            : !string.IsNullOrWhiteSpace(tenantId)
                ? $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId.Trim())}/oauth2/v2.0/token"
                : string.Empty;

        if (!Uri.TryCreate(tokenEndpointUrl, UriKind.Absolute, out var tokenUri))
        {
            return new TokenResolutionResult(
                false,
                ErrorCode: "SHAREPOINT_OAUTH2_CONFIG_INVALID",
                ErrorMessage: "SharePoint OAuth2 tokenEndpoint or tenantId is missing or invalid.");
        }

        // Validate tokenEndpoint through ISsrfGuard (blocks private/link-local IPs & unwhitelisted custom hosts)
        var tokenAllowedHosts = new List<string>(connection.GetAllowedHosts());
        if (string.IsNullOrWhiteSpace(explicitTokenEndpoint) &&
            !tokenAllowedHosts.Contains("login.microsoftonline.com", StringComparer.OrdinalIgnoreCase))
        {
            tokenAllowedHosts.Add("login.microsoftonline.com");
        }

        var tokenSsrf = await _ssrfGuard.ValidateAsync(tokenUri, tokenAllowedHosts, ct);
        if (!tokenSsrf.IsAllowed)
        {
            return new TokenResolutionResult(
                false,
                ErrorCode: "SSRF_BLOCKED",
                ErrorMessage: tokenSsrf.BlockReason ?? "OAuth2 token endpoint blocked by SSRF guard.");
        }

        string cacheKey = $"sp-oauth2-token:{connection.Id}:{tokenEndpointUrl}:{clientId}:{scope}";
        if (_memoryCache.TryGetValue<string>(cacheKey, out var cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
        {
            return new TokenResolutionResult(true, cachedToken);
        }

        var tokenClient = _httpClientFactory.CreateClient(TokenHttpClientName);
        using var tokenReq = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = scope
            })
        };

        using var tokenResp = await tokenClient.SendAsync(tokenReq, HttpCompletionOption.ResponseHeadersRead, ct);
        int status = (int)tokenResp.StatusCode;

        if (IsRedirectStatusCode(status))
        {
            return new TokenResolutionResult(
                false,
                ErrorCode: "SSRF_REDIRECT_BLOCKED",
                ErrorMessage: $"OAuth2 token endpoint attempted an HTTP {status} redirect, which is blocked by SSRF policy.");
        }

        var (tokenBody, sizeExceeded) = await ReadBoundedResponseAsync(tokenResp.Content, 256 * 1024, ct);
        if (sizeExceeded)
        {
            return new TokenResolutionResult(false, ErrorCode: "SHAREPOINT_TOKEN_RESPONSE_TOO_LARGE", ErrorMessage: "OAuth2 token response exceeded size limit.");
        }

        if (!tokenResp.IsSuccessStatusCode)
        {
            return new TokenResolutionResult(
                false,
                ErrorCode: "SHAREPOINT_TOKEN_ERROR",
                ErrorMessage: $"OAuth2 token acquisition failed with HTTP {status}.");
        }

        using var doc = JsonDocument.Parse(tokenBody);
        if (!doc.RootElement.TryGetProperty("access_token", out var accessTokenEl) ||
            string.IsNullOrWhiteSpace(accessTokenEl.GetString()))
        {
            return new TokenResolutionResult(
                false,
                ErrorCode: "SHAREPOINT_TOKEN_ERROR",
                ErrorMessage: "OAuth2 token response did not contain an access_token.");
        }

        string accessToken = accessTokenEl.GetString()!;
        int expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expEl) && expEl.TryGetInt32(out int expSec)
            ? expSec
            : 3600;

        var cacheDuration = TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60));
        _memoryCache.Set(cacheKey, accessToken, cacheDuration);

        return new TokenResolutionResult(true, accessToken);
    }

    private static string BuildGraphPath(
        string operation,
        string? rawPath,
        string? listIdOrName,
        IReadOnlyDictionary<string, object?> parameters,
        int effectiveMaxRows)
    {
        string path;
        if (!string.IsNullOrWhiteSpace(rawPath))
        {
            path = rawPath.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(listIdOrName))
        {
            string encodedList = Uri.EscapeDataString(listIdOrName.Trim());
            string? itemId = ExtractString(parameters, "itemId") ?? ExtractString(parameters, "id");
            path = operation switch
            {
                "get" or "update" or "delete" when !string.IsNullOrWhiteSpace(itemId) =>
                    $"lists/{encodedList}/items/{Uri.EscapeDataString(itemId)}",
                _ => $"lists/{encodedList}/items"
            };
        }
        else
        {
            path = "lists";
        }

        if (operation is "list")
        {
            int requestedTop = ExtractInt(parameters, "pageSize",
                ExtractInt(parameters, "top",
                    ExtractInt(parameters, "limit", effectiveMaxRows, 1, HardMaxRows),
                    1, HardMaxRows),
                1, HardMaxRows);

            int top = Math.Clamp(Math.Min(requestedTop, effectiveMaxRows), 1, HardMaxRows);
            path = EnforceTopQueryParameter(path, top);

            string? skipToken = ExtractString(parameters, "skipToken") ?? ExtractString(parameters, "$skiptoken");
            if (!string.IsNullOrWhiteSpace(skipToken) &&
                !path.Contains("$skiptoken=", StringComparison.OrdinalIgnoreCase))
            {
                path += (path.Contains('?') ? "&" : "?") + $"$skiptoken={Uri.EscapeDataString(skipToken)}";
            }

            if (path.Contains("/items", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains("expand=", StringComparison.OrdinalIgnoreCase))
            {
                path += (path.Contains('?') ? "&" : "?") + "expand=fields";
            }
        }

        return path;
    }

    private static string EnforceTopQueryParameter(string path, int enforcedTop)
    {
        int qIdx = path.IndexOf('?');
        if (qIdx < 0)
        {
            return $"{path}?$top={enforcedTop}";
        }

        string basePart = path[..qIdx];
        string queryPart = path[(qIdx + 1)..];
        var segments = queryPart.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(seg => !seg.StartsWith("$top=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        segments.Add($"$top={enforcedTop}");

        return $"{basePart}?{string.Join("&", segments)}";
    }

    private static bool TryBuildTargetUri(string baseUrl, string? path, out Uri? uri, out string? error)
    {
        uri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            error = "SharePoint connection BaseUrl must be an absolute HTTP or HTTPS URL (e.g. https://graph.microsoft.com/v1.0/sites/{siteId}).";
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

        string combined = $"{baseUrl.TrimEnd('/')}/{path.Trim().TrimStart('/')}";
        if (!Uri.TryCreate(combined, UriKind.Absolute, out var built))
        {
            error = $"Unable to combine BaseUrl with SharePoint path '{path}'.";
            return false;
        }

        uri = built;
        return true;
    }

    private static void ApplyHeaders(
        HttpRequestMessage request,
        Connection connection,
        string? accessToken,
        IReadOnlyDictionary<string, string>? extraHeaders)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    private static bool IsRedirectStatusCode(int statusCode) =>
        statusCode is >= 300 and <= 399;

    private static async Task<(string Body, bool SizeExceeded)> ReadBoundedResponseAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken ct)
    {
        if (content.Headers.ContentLength.HasValue && content.Headers.ContentLength.Value > maxBytes)
        {
            return (string.Empty, true);
        }

        await using var stream = await content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        int totalRead = 0;

        while (true)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > maxBytes)
            {
                return (string.Empty, true);
            }

            ms.Write(buffer, 0, read);
        }

        return (Encoding.UTF8.GetString(ms.ToArray()), false);
    }

    private static List<Dictionary<string, object?>>? ExtractGraphRows(JsonElement doc, int maxRows)
    {
        if (doc.ValueKind != JsonValueKind.Object ||
            !doc.TryGetProperty("value", out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var rows = new List<Dictionary<string, object?>>();
        foreach (var item in valueEl.EnumerateArray())
        {
            if (rows.Count >= maxRows)
            {
                break;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in item.EnumerateObject())
            {
                row[prop.Name] = UnwrapJsonValue(prop.Value);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static object? UnwrapJsonValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => el.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => el.TryGetInt32(out int i) ? i
            : el.TryGetInt64(out long l) ? l
            : el.GetDecimal(),
        JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => UnwrapJsonValue(p.Value), StringComparer.Ordinal),
        JsonValueKind.Array => el.EnumerateArray().Select(UnwrapJsonValue).ToList(),
        _ => el.GetRawText()
    };

    private static string? ExtractString(IReadOnlyDictionary<string, object?> parameters, string key)
    {
        var match = parameters.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
        if (match.Key == null || match.Value == null) return null;
        return match.Value is JsonElement el
            ? (el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText())
            : match.Value.ToString();
    }

    private static int ExtractInt(IReadOnlyDictionary<string, object?> parameters, string key, int defaultValue, int min, int max)
    {
        string? raw = ExtractString(parameters, key);
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out int parsed))
        {
            return Math.Clamp(parsed, min, max);
        }

        return defaultValue;
    }
}
