using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WorkflowEngine.Application.Services.Security;

namespace WorkflowEngine.Api.Auth;

public static class OidcHttpClientNames
{
    public const string OidcProvider = "OidcProvider";
}

public sealed record OidcDiscoveryDocument(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("jwks_uri")] string JwksUri
);

public interface IOidcDiscoveryCache
{
    Task<OidcDiscoveryDocument> GetDiscoveryAsync(string authority, bool forceRefresh = false, CancellationToken ct = default);
    Task<JsonWebKeySet> GetJwksAsync(string authority, bool forceRefresh = false, CancellationToken ct = default);
    void Invalidate(string authority);
}

/// <summary>
/// Caches OIDC Discovery metadata (.well-known/openid-configuration) and JWKS keys with a 15-minute TTL.
/// Enforces HTTPS and ISsrfGuard on every outbound metadata/JWKS fetch and supports on-demand invalidation
/// for IdP signing key rotation.
/// </summary>
public sealed class OidcDiscoveryCache : IOidcDiscoveryCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly ILogger<OidcDiscoveryCache> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public OidcDiscoveryCache(
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ISsrfGuard ssrfGuard,
        ILogger<OidcDiscoveryCache> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _ssrfGuard = ssrfGuard ?? throw new ArgumentNullException(nameof(ssrfGuard));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OidcDiscoveryDocument> GetDiscoveryAsync(
        string authority,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        string normalized = NormalizeAuthority(authority);
        string cacheKey = $"oidc:discovery:{normalized}";

        if (!forceRefresh && _cache.TryGetValue(cacheKey, out OidcDiscoveryDocument? cached) && cached != null)
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                return cached;
            }

            var discoveryUri = new Uri($"{normalized}/.well-known/openid-configuration", UriKind.Absolute);
            await EnsureSafeHttpsUriAsync(discoveryUri, ct);

            var client = _httpClientFactory.CreateClient(OidcHttpClientNames.OidcProvider);
            using var response = await client.GetAsync(discoveryUri, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch OIDC discovery document from '{normalized}' (HTTP {(int)response.StatusCode}).");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonSerializer.Deserialize<OidcDiscoveryDocument>(json, JsonOptions)
                ?? throw new InvalidOperationException($"OIDC discovery document from '{normalized}' was empty.");

            if (string.IsNullOrWhiteSpace(doc.Issuer) ||
                string.IsNullOrWhiteSpace(doc.AuthorizationEndpoint) ||
                string.IsNullOrWhiteSpace(doc.TokenEndpoint) ||
                string.IsNullOrWhiteSpace(doc.JwksUri))
            {
                throw new InvalidOperationException(
                    $"OIDC discovery document from '{normalized}' is missing required endpoints (issuer, authorization_endpoint, token_endpoint, jwks_uri).");
            }

            _cache.Set(cacheKey, doc, CacheTtl);
            _logger.LogInformation("Cached OIDC discovery metadata for authority {Authority}.", normalized);
            return doc;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<JsonWebKeySet> GetJwksAsync(
        string authority,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        string normalized = NormalizeAuthority(authority);
        string cacheKey = $"oidc:jwks:{normalized}";

        if (!forceRefresh && _cache.TryGetValue(cacheKey, out JsonWebKeySet? cached) && cached != null)
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out cached) && cached != null)
            {
                return cached;
            }

            var discovery = await GetDiscoveryAsync(normalized, forceRefresh, ct);
            if (!Uri.TryCreate(discovery.JwksUri, UriKind.Absolute, out var jwksUri))
            {
                throw new InvalidOperationException($"OIDC jwks_uri '{discovery.JwksUri}' is not a valid absolute URI.");
            }

            await EnsureSafeHttpsUriAsync(jwksUri, ct);

            var client = _httpClientFactory.CreateClient(OidcHttpClientNames.OidcProvider);
            using var response = await client.GetAsync(jwksUri, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch OIDC JWKS from '{jwksUri.GetLeftPart(UriPartial.Path)}' (HTTP {(int)response.StatusCode}).");
            }

            var jwksJson = await response.Content.ReadAsStringAsync(ct);
            var jwks = new JsonWebKeySet(jwksJson);

            _cache.Set(cacheKey, jwks, CacheTtl);
            _logger.LogInformation("Cached OIDC JWKS ({KeyCount} keys) for authority {Authority}.", jwks.Keys.Count, normalized);
            return jwks;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Invalidate(string authority)
    {
        string normalized = NormalizeAuthority(authority);
        _cache.Remove($"oidc:discovery:{normalized}");
        _cache.Remove($"oidc:jwks:{normalized}");
        _logger.LogInformation("Invalidated OIDC discovery and JWKS cache for authority {Authority}.", normalized);
    }

    private async Task EnsureSafeHttpsUriAsync(Uri uri, CancellationToken ct)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"OIDC endpoint '{uri.Host}' must use HTTPS.");
        }

        var result = await _ssrfGuard.ValidateAsync(uri, new[] { uri.DnsSafeHost }, ct);
        if (!result.IsAllowed)
        {
            throw new InvalidOperationException($"OIDC endpoint blocked by SSRF guard: {result.BlockReason}");
        }
    }

    private static string NormalizeAuthority(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new ArgumentException("Authority is required.", nameof(authority));
        }

        return authority.Trim().TrimEnd('/');
    }
}
