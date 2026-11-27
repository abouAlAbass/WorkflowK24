using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Performs OAuth2 On-Behalf-Of (OBO) token exchange (RFC 8693 / Microsoft Entra ID jwt-bearer flow):
/// 1. Loads the user's encrypted ExternalUserToken (protected with SecretPurposes.ExternalTokens).
/// 2. Refreshes the user's access token via EncryptedRefreshToken when expired (or within 60s of expiry).
/// 3. Exchanges the user's external access token with the IdP token endpoint using
///    grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer and requested_token_use=on_behalf_of.
/// 4. Throws ExternalReauthRequiredException (422 EXTERNAL_REAUTH_REQUIRED) if no valid token is available.
/// </summary>
public class OboTokenExchangeService : IOboTokenExchangeService
{
    public const string HttpClientName = "OboTokenExchange";
    public const string OboGrantType = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    public const string DefaultSharePointScope = "https://graph.microsoft.com/.default";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IExternalUserTokenRepository _tokenRepository;
    private readonly IAuthProviderRepository _authProviderRepository;
    private readonly ISystemUserRepository _userRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<OboTokenExchangeService> _logger;

    public OboTokenExchangeService(
        IExternalUserTokenRepository tokenRepository,
        IAuthProviderRepository authProviderRepository,
        ISystemUserRepository userRepository,
        ISecretProtector secretProtector,
        ISsrfGuard ssrfGuard,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<OboTokenExchangeService> logger)
    {
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _authProviderRepository = authProviderRepository ?? throw new ArgumentNullException(nameof(authProviderRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
        _ssrfGuard = ssrfGuard ?? throw new ArgumentNullException(nameof(ssrfGuard));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OboTokenExchangeResult> ExchangeForUserAsync(
        Connection connection,
        string userId,
        IEnumerable<string>? alternateUserIds = null,
        ResolvedExecutionSecret? connectionClientCredentials = null,
        string? targetScope = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var candidateUserIds = await BuildCandidateUserIdsAsync(userId, alternateUserIds, ct);
        string effectiveUserId = candidateUserIds.FirstOrDefault() ?? "unknown";
        string? preferredProviderId = connectionClientCredentials?.GetField("providerId", "provider_id");

        var storedToken = candidateUserIds.Count > 0
            ? await _tokenRepository.GetByAnyUserIdAsync(candidateUserIds, preferredProviderId, ct)
            : null;

        if (storedToken == null || string.IsNullOrWhiteSpace(storedToken.EncryptedAccessToken))
        {
            throw new ExternalReauthRequiredException(connection.Id, connection.Name, effectiveUserId);
        }

        string userAccessToken;
        string? userRefreshToken = null;
        try
        {
            userAccessToken = _secretProtector.Unprotect(storedToken.EncryptedAccessToken, SecretPurposes.ExternalTokens);
            if (!string.IsNullOrWhiteSpace(storedToken.EncryptedRefreshToken))
            {
                userRefreshToken = _secretProtector.Unprotect(storedToken.EncryptedRefreshToken, SecretPurposes.ExternalTokens);
            }
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt ExternalUserToken for user {UserId}.", effectiveUserId);
            throw new ExternalReauthRequiredException(connection.Id, connection.Name, effectiveUserId);
        }

        if (string.IsNullOrWhiteSpace(userAccessToken))
        {
            throw new ExternalReauthRequiredException(connection.Id, connection.Name, effectiveUserId);
        }

        var provider = await _authProviderRepository.GetByIdAsync(storedToken.ProviderId, ct);

        string? providerClientSecret = null;
        if (!string.IsNullOrWhiteSpace(provider?.EncryptedClientSecret))
        {
            try
            {
                providerClientSecret = _secretProtector.Unprotect(provider.EncryptedClientSecret, SecretPurposes.AuthProviders);
            }
            catch (CryptographicException)
            {
                providerClientSecret = null;
            }
        }

        string scope = !string.IsNullOrWhiteSpace(targetScope)
            ? targetScope.Trim()
            : connectionClientCredentials?.GetField("scope")
                ?? (string.Equals(connection.ServiceType, "sharepoint", StringComparison.OrdinalIgnoreCase)
                    ? DefaultSharePointScope
                    : "openid");

        string clientId = connectionClientCredentials?.GetField("clientId", "client_id")
            ?? provider?.ClientId
            ?? string.Empty;
        string? clientSecret = connectionClientCredentials?.GetField("clientSecret", "client_secret")
            ?? providerClientSecret;

        // 1. Refresh expired external user access token if needed
        if (storedToken.ExpiresAt <= DateTime.UtcNow.AddSeconds(60))
        {
            if (string.IsNullOrWhiteSpace(userRefreshToken))
            {
                throw new ExternalReauthRequiredException(
                    connection.Id,
                    connection.Name,
                    effectiveUserId,
                    $"{ExternalReauthRequiredException.ErrorCode}: External token for user '{effectiveUserId}' is expired and no refresh token is available.");
            }

            var refreshEndpoint = await ResolveTokenEndpointAsync(provider, connectionClientCredentials, connection, ct);
            if (refreshEndpoint == null)
            {
                throw new ExternalReauthRequiredException(connection.Id, connection.Name, effectiveUserId);
            }

            var refreshed = await RefreshUserAccessTokenAsync(
                refreshEndpoint,
                provider?.ClientId ?? clientId,
                providerClientSecret ?? clientSecret,
                userRefreshToken,
                connection.GetAllowedHosts(),
                ct);

            if (refreshed == null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
            {
                throw new ExternalReauthRequiredException(
                    connection.Id,
                    connection.Name,
                    effectiveUserId,
                    $"{ExternalReauthRequiredException.ErrorCode}: Failed to refresh expired external token for user '{effectiveUserId}'.");
            }

            userAccessToken = refreshed.AccessToken;
            var newExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn is > 0 ? refreshed.ExpiresIn.Value : 3600);
            string protectedAccess = _secretProtector.Protect(refreshed.AccessToken, SecretPurposes.ExternalTokens);
            string? protectedRefresh = !string.IsNullOrWhiteSpace(refreshed.RefreshToken)
                ? _secretProtector.Protect(refreshed.RefreshToken, SecretPurposes.ExternalTokens)
                : null;

            storedToken.UpdateTokens(protectedAccess, protectedRefresh, newExpiresAt, preserveExistingRefreshTokenIfNull: true);
            await _tokenRepository.UpdateAsync(storedToken, ct);
        }

        // 2. Check OBO cache
        string cacheKey = $"obo-token:{connection.Id}:{storedToken.UserId}:{scope}";
        if (_memoryCache.TryGetValue<OboTokenExchangeResult>(cacheKey, out var cached) &&
            cached != null &&
            cached.ExpiresAt > DateTime.UtcNow.AddSeconds(30))
        {
            return cached;
        }

        // 3. Resolve OBO token endpoint and execute OAuth2 jwt-bearer on_behalf_of exchange
        var tokenEndpointUri = await ResolveTokenEndpointAsync(provider, connectionClientCredentials, connection, ct);
        if (tokenEndpointUri == null)
        {
            throw new ExternalReauthRequiredException(
                connection.Id,
                connection.Name,
                effectiveUserId,
                $"{ExternalReauthRequiredException.ErrorCode}: Unable to resolve OIDC/OAuth2 token endpoint for OBO exchange on connection '{connection.Name}'.");
        }

        var allowedHosts = BuildTokenAllowedHosts(connection, tokenEndpointUri);
        var ssrf = await _ssrfGuard.ValidateAsync(tokenEndpointUri, allowedHosts, ct);
        if (!ssrf.IsAllowed)
        {
            throw new InvalidOperationException($"OBO token endpoint blocked by SSRF guard: {ssrf.BlockReason}");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = OboGrantType,
            ["requested_token_use"] = "on_behalf_of",
            ["assertion"] = userAccessToken,
            ["scope"] = scope
        };

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            form["client_id"] = clientId;
        }

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            form["client_secret"] = clientSecret;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpointUri)
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        int status = (int)resp.StatusCode;
        if (status is >= 300 and <= 399)
        {
            throw new InvalidOperationException($"OBO token endpoint attempted an HTTP {status} redirect, blocked by SSRF policy.");
        }

        string body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "OBO token exchange failed with HTTP {StatusCode} for connection {ConnectionId} and user {UserId}.",
                status, connection.Id, storedToken.UserId);
            throw new ExternalReauthRequiredException(
                connection.Id,
                connection.Name,
                effectiveUserId,
                $"{ExternalReauthRequiredException.ErrorCode}: Identity provider rejected OBO token exchange (HTTP {status}).");
        }

        var tokenPayload = JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions);
        if (tokenPayload == null || string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            throw new ExternalReauthRequiredException(
                connection.Id,
                connection.Name,
                effectiveUserId,
                $"{ExternalReauthRequiredException.ErrorCode}: Identity provider OBO response did not contain an access_token.");
        }

        int expiresIn = tokenPayload.ExpiresIn is > 0 ? tokenPayload.ExpiresIn.Value : 3600;
        var result = new OboTokenExchangeResult(
            AccessToken: tokenPayload.AccessToken,
            ExpiresAt: DateTime.UtcNow.AddSeconds(expiresIn),
            Scope: tokenPayload.Scope ?? scope);

        var cacheDuration = TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60));
        _memoryCache.Set(cacheKey, result, cacheDuration);

        return result;
    }

    private async Task<List<string>> BuildCandidateUserIdsAsync(
        string? userId,
        IEnumerable<string>? alternateUserIds,
        CancellationToken ct)
    {
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            ids.Add(userId.Trim());
        }

        if (alternateUserIds != null)
        {
            foreach (var alt in alternateUserIds)
            {
                if (!string.IsNullOrWhiteSpace(alt))
                {
                    ids.Add(alt.Trim());
                }
            }
        }

        if (ids.Count > 0)
        {
            string primary = ids[0];
            var user = await _userRepository.GetByIdAsync(primary, ct)
                ?? await _userRepository.GetByEmailAsync(primary, ct);
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(user.Id)) ids.Add(user.Id);
                if (!string.IsNullOrWhiteSpace(user.Email)) ids.Add(user.Email);
            }
        }

        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<Uri?> ResolveTokenEndpointAsync(
        AuthProvider? provider,
        ResolvedExecutionSecret? connectionCreds,
        Connection connection,
        CancellationToken ct)
    {
        string? explicitEndpoint = connectionCreds?.GetField("tokenEndpoint", "token_endpoint");
        if (!string.IsNullOrWhiteSpace(explicitEndpoint) &&
            Uri.TryCreate(explicitEndpoint.Trim(), UriKind.Absolute, out var explicitUri))
        {
            return explicitUri;
        }

        string? tenantId = connectionCreds?.GetField("tenantId", "tenant_id");
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var entraUrl = $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId.Trim())}/oauth2/v2.0/token";
            if (Uri.TryCreate(entraUrl, UriKind.Absolute, out var entraUri))
            {
                return entraUri;
            }
        }

        if (provider != null && !string.IsNullOrWhiteSpace(provider.Authority))
        {
            string authority = provider.Authority.Trim().TrimEnd('/');
            string discoveryCacheKey = $"obo-discovery-token-endpoint:{authority}";
            if (_memoryCache.TryGetValue<Uri>(discoveryCacheKey, out var cachedEndpoint) && cachedEndpoint != null)
            {
                return cachedEndpoint;
            }

            if (Uri.TryCreate($"{authority}/.well-known/openid-configuration", UriKind.Absolute, out var discoveryUri))
            {
                var ssrf = await _ssrfGuard.ValidateAsync(discoveryUri, new[] { discoveryUri.DnsSafeHost }, ct);
                if (ssrf.IsAllowed)
                {
                    var client = _httpClientFactory.CreateClient(HttpClientName);
                    using var resp = await client.GetAsync(discoveryUri, ct);
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync(ct);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("token_endpoint", out var te) &&
                            Uri.TryCreate(te.GetString(), UriKind.Absolute, out var discoveredUri))
                        {
                            _memoryCache.Set(discoveryCacheKey, discoveredUri, TimeSpan.FromMinutes(15));
                            return discoveredUri;
                        }
                    }
                }
            }

            if (Uri.TryCreate($"{authority}/token", UriKind.Absolute, out var fallbackUri))
            {
                return fallbackUri;
            }
        }

        return null;
    }

    private async Task<OAuthTokenResponse?> RefreshUserAccessTokenAsync(
        Uri tokenEndpointUri,
        string clientId,
        string? clientSecret,
        string refreshToken,
        IReadOnlyList<string> connectionAllowedHosts,
        CancellationToken ct)
    {
        var allowedHosts = new List<string>(connectionAllowedHosts);
        if (!allowedHosts.Contains(tokenEndpointUri.DnsSafeHost, StringComparer.OrdinalIgnoreCase))
        {
            allowedHosts.Add(tokenEndpointUri.DnsSafeHost);
        }

        var ssrf = await _ssrfGuard.ValidateAsync(tokenEndpointUri, allowedHosts, ct);
        if (!ssrf.IsAllowed)
        {
            return null;
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            form["client_id"] = clientId;
        }
        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            form["client_secret"] = clientSecret;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpointUri)
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        string body = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<OAuthTokenResponse>(body, JsonOptions);
    }

    private static List<string> BuildTokenAllowedHosts(Connection connection, Uri tokenEndpointUri)
    {
        var allowedHosts = new List<string>(connection.GetAllowedHosts());
        if (!allowedHosts.Contains(tokenEndpointUri.DnsSafeHost, StringComparer.OrdinalIgnoreCase))
        {
            allowedHosts.Add(tokenEndpointUri.DnsSafeHost);
        }
        return allowedHosts;
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}
