using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace WorkflowEngine.Api.Auth;

public sealed record OidcChallengeState(
    string ProviderId,
    string Nonce,
    string CodeVerifier,
    string RedirectUri,
    string? ReturnUrl,
    DateTime CreatedAtUtc
);

public sealed record OidcExchangePayload(
    string UserId,
    string Name,
    string Email,
    string Role,
    string Group,
    string ProviderId
);

public interface IOidcFlowStateStore
{
    string StoreChallenge(OidcChallengeState state);
    OidcChallengeState? ConsumeChallenge(string stateKey);

    string StoreExchangeCode(OidcExchangePayload payload);
    OidcExchangePayload? ConsumeExchangeCode(string exchangeCode);
}

/// <summary>
/// Ephemeral, single-use state store for OIDC PKCE/nonce challenges (5 min TTL)
/// and opaque one-time login exchange codes (60s TTL).
/// </summary>
public sealed class OidcFlowStateStore : IOidcFlowStateStore
{
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ExchangeCodeTtl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly object _consumeLock = new();

    public OidcFlowStateStore(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public string StoreChallenge(OidcChallengeState state)
    {
        string key = GenerateOpaqueToken();
        _cache.Set(ChallengeKey(key), state, ChallengeTtl);
        return key;
    }

    public OidcChallengeState? ConsumeChallenge(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey)) return null;

        lock (_consumeLock)
        {
            string fullKey = ChallengeKey(stateKey);
            if (!_cache.TryGetValue(fullKey, out OidcChallengeState? state) || state == null)
            {
                return null;
            }

            _cache.Remove(fullKey);
            return state;
        }
    }

    public string StoreExchangeCode(OidcExchangePayload payload)
    {
        string code = GenerateOpaqueToken();
        _cache.Set(ExchangeKey(code), payload, ExchangeCodeTtl);
        return code;
    }

    public OidcExchangePayload? ConsumeExchangeCode(string exchangeCode)
    {
        if (string.IsNullOrWhiteSpace(exchangeCode)) return null;

        lock (_consumeLock)
        {
            string fullKey = ExchangeKey(exchangeCode);
            if (!_cache.TryGetValue(fullKey, out OidcExchangePayload? payload) || payload == null)
            {
                return null;
            }

            _cache.Remove(fullKey);
            return payload;
        }
    }

    public static string GenerateOpaqueToken(int numBytes = 32)
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(numBytes);
        return Base64UrlEncode(bytes);
    }

    public static string ComputeS256CodeChallenge(string codeVerifier)
    {
        byte[] hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string ChallengeKey(string key) => $"oidc:challenge:{key}";
    private static string ExchangeKey(string code) => $"oidc:exchange:{code}";
}
