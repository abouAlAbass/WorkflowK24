using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Services.Security;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Api.Auth;

public sealed record OidcChallengeResult(
    bool Success,
    string? AuthorizationUrl = null,
    int StatusCode = 302,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record OidcCallbackResult(
    bool Success,
    string? FrontendRedirectUrl = null,
    int StatusCode = 302,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface IOidcAuthenticationService
{
    Task<OidcChallengeResult> StartChallengeAsync(
        string providerId,
        string callbackUri,
        string? returnUrl,
        CancellationToken ct = default);

    Task<OidcCallbackResult> HandleCallbackAsync(
        string providerId,
        string? code,
        string? state,
        string? error,
        CancellationToken ct = default);
}

public sealed class OidcAuthenticationService : IOidcAuthenticationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IAuthProviderRepository _providers;
    private readonly IExternalUserLoginRepository _externalLogins;
    private readonly IExternalUserTokenRepository _externalTokens;
    private readonly ISystemUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretProtector _secretProtector;
    private readonly ISsrfGuard _ssrfGuard;
    private readonly IOidcDiscoveryCache _discoveryCache;
    private readonly IOidcFlowStateStore _stateStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OidcAuthenticationService> _logger;

    public OidcAuthenticationService(
        IAuthProviderRepository providers,
        IExternalUserLoginRepository externalLogins,
        IExternalUserTokenRepository externalTokens,
        ISystemUserRepository users,
        IUnitOfWork unitOfWork,
        ISecretProtector secretProtector,
        ISsrfGuard ssrfGuard,
        IOidcDiscoveryCache discoveryCache,
        IOidcFlowStateStore stateStore,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OidcAuthenticationService> logger)
    {
        _providers = providers;
        _externalLogins = externalLogins;
        _externalTokens = externalTokens;
        _users = users;
        _unitOfWork = unitOfWork;
        _secretProtector = secretProtector;
        _ssrfGuard = ssrfGuard;
        _discoveryCache = discoveryCache;
        _stateStore = stateStore;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OidcChallengeResult> StartChallengeAsync(
        string providerId,
        string callbackUri,
        string? returnUrl,
        CancellationToken ct = default)
    {
        var provider = await _providers.GetByIdAsync(providerId, ct);
        if (provider == null || !provider.IsEnabled)
        {
            return new OidcChallengeResult(false, StatusCode: 404, ErrorCode: "PROVIDER_NOT_FOUND", ErrorMessage: "Auth provider not found or disabled.");
        }

        OidcDiscoveryDocument discovery;
        try
        {
            discovery = await _discoveryCache.GetDiscoveryAsync(provider.Authority, false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OIDC discovery failed for provider {ProviderId}.", provider.Id);
            return new OidcChallengeResult(false, StatusCode: 502, ErrorCode: "OIDC_DISCOVERY_FAILED", ErrorMessage: "Failed to load OIDC provider discovery metadata.");
        }

        string nonce = OidcFlowStateStore.GenerateOpaqueToken();
        string codeVerifier = OidcFlowStateStore.GenerateOpaqueToken(32);
        string codeChallenge = OidcFlowStateStore.ComputeS256CodeChallenge(codeVerifier);

        string state = _stateStore.StoreChallenge(new OidcChallengeState(
            provider.Id,
            nonce,
            codeVerifier,
            callbackUri,
            returnUrl,
            DateTime.UtcNow));

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = provider.ClientId,
            ["redirect_uri"] = callbackUri,
            ["scope"] = provider.Scopes,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var separator = discovery.AuthorizationEndpoint.Contains('?') ? "&" : "?";
        string authorizationUrl = $"{discovery.AuthorizationEndpoint}{separator}{queryString}";

        return new OidcChallengeResult(true, AuthorizationUrl: authorizationUrl, StatusCode: 302);
    }

    public async Task<OidcCallbackResult> HandleCallbackAsync(
        string providerId,
        string? code,
        string? state,
        string? error,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return new OidcCallbackResult(false, StatusCode: 400, ErrorCode: "OIDC_PROVIDER_ERROR", ErrorMessage: $"Identity provider returned error: {error}");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return new OidcCallbackResult(false, StatusCode: 400, ErrorCode: "INVALID_CALLBACK_PARAMETERS", ErrorMessage: "Both 'code' and 'state' are required.");
        }

        var challenge = _stateStore.ConsumeChallenge(state);
        if (challenge == null || !string.Equals(challenge.ProviderId, providerId, StringComparison.Ordinal))
        {
            return new OidcCallbackResult(false, StatusCode: 400, ErrorCode: "INVALID_OR_EXPIRED_STATE", ErrorMessage: "OIDC state is invalid, expired, or already used.");
        }

        var provider = await _providers.GetByIdAsync(providerId, ct);
        if (provider == null || !provider.IsEnabled)
        {
            return new OidcCallbackResult(false, StatusCode: 404, ErrorCode: "PROVIDER_NOT_FOUND", ErrorMessage: "Auth provider not found or disabled.");
        }

        OidcDiscoveryDocument discovery;
        try
        {
            discovery = await _discoveryCache.GetDiscoveryAsync(provider.Authority, false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OIDC discovery failed during callback for provider {ProviderId}.", provider.Id);
            return new OidcCallbackResult(false, StatusCode: 502, ErrorCode: "OIDC_DISCOVERY_FAILED", ErrorMessage: "Failed to load OIDC provider discovery metadata.");
        }

        // 1. Exchange authorization code + PKCE code_verifier (+ decrypted client_secret)
        OidcTokenEndpointResponse? tokenResponse;
        try
        {
            tokenResponse = await ExchangeCodeForTokensAsync(provider, discovery, code, challenge, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OIDC token exchange failed for provider {ProviderId}.", provider.Id);
            return new OidcCallbackResult(false, StatusCode: 401, ErrorCode: "TOKEN_EXCHANGE_FAILED", ErrorMessage: "Failed to exchange authorization code with identity provider.");
        }

        string? idToken = tokenResponse?.IdToken;
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return new OidcCallbackResult(false, StatusCode: 401, ErrorCode: "MISSING_ID_TOKEN", ErrorMessage: "Identity provider token response did not contain an id_token.");
        }

        // 2. Validate id_token signature (with 1x automatic JWKS refresh on key rotation), iss, aud, exp, nonce
        ClaimsPrincipal? principal;
        try
        {
            principal = await ValidateIdTokenAsync(provider, discovery, idToken, challenge.Nonce, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OIDC id_token validation failed for provider {ProviderId}.", provider.Id);
            return new OidcCallbackResult(false, StatusCode: 401, ErrorCode: "INVALID_ID_TOKEN", ErrorMessage: "OIDC id_token validation failed.");
        }

        string? sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return new OidcCallbackResult(false, StatusCode: 401, ErrorCode: "MISSING_SUBJECT", ErrorMessage: "OIDC id_token is missing the required 'sub' claim.");
        }

        string? email = (principal.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? principal.FindFirstValue(ClaimTypes.Email))?.Trim().ToLowerInvariant();
        bool emailVerified = IsEmailVerified(principal);
        string displayName = principal.FindFirstValue("name")
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue("preferred_username")
            ?? email
            ?? sub;
        var externalGroups = ExtractExternalGroupsAndRoles(principal);

        // 3. Resolve user: (providerId, sub) -> verified email / AllowEmailMatching -> JIT provisioning
        var resolution = await ResolveOrProvisionUserAsync(provider, sub, email, emailVerified, displayName, externalGroups, ct);
        if (!resolution.Success || resolution.User == null)
        {
            return new OidcCallbackResult(false, StatusCode: resolution.StatusCode, ErrorCode: resolution.ErrorCode, ErrorMessage: resolution.ErrorMessage);
        }

        var user = resolution.User;
        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return new OidcCallbackResult(false, StatusCode: 403, ErrorCode: "USER_INACTIVE", ErrorMessage: "User account is inactive.");
        }

        // 3b. Persist encrypted external IdP access/refresh tokens under SecretPurposes.ExternalTokens (NEVER in internal JWT)
        if (!string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            string encryptedAccess = _secretProtector.Protect(tokenResponse.AccessToken, SecretPurposes.ExternalTokens);
            string? encryptedRefresh = !string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
                ? _secretProtector.Protect(tokenResponse.RefreshToken, SecretPurposes.ExternalTokens)
                : null;
            var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn is > 0 ? tokenResponse.ExpiresIn.Value : 3600);

            var existingToken = await _externalTokens.GetByUserAndProviderAsync(user.Id, provider.Id, ct);
            if (existingToken != null)
            {
                existingToken.UpdateTokens(encryptedAccess, encryptedRefresh, expiresAt, preserveExistingRefreshTokenIfNull: true);
                await _externalTokens.UpdateAsync(existingToken, ct);
            }
            else
            {
                await _externalTokens.AddAsync(
                    new ExternalUserToken(user.Id, provider.Id, encryptedAccess, encryptedRefresh, expiresAt),
                    ct);
            }
        }

        // 4. Issue opaque one-time exchange code (TTL 60s) - NEVER put JWT in redirect URL
        string exchangeCode = _stateStore.StoreExchangeCode(new OidcExchangePayload(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.Group,
            provider.Id));

        string frontendBase = ResolveFrontendBaseUrl();
        string redirectUrl = $"{frontendBase}/login/callback?exchangeCode={Uri.EscapeDataString(exchangeCode)}";

        return new OidcCallbackResult(true, FrontendRedirectUrl: redirectUrl, StatusCode: 302);
    }

    private async Task<OidcTokenEndpointResponse?> ExchangeCodeForTokensAsync(
        AuthProvider provider,
        OidcDiscoveryDocument discovery,
        string code,
        OidcChallengeState challenge,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(discovery.TokenEndpoint, UriKind.Absolute, out var tokenUri) ||
            !string.Equals(tokenUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("OIDC token_endpoint must be a valid HTTPS URL.");
        }

        var ssrf = await _ssrfGuard.ValidateAsync(tokenUri, new[] { tokenUri.DnsSafeHost }, ct);
        if (!ssrf.IsAllowed)
        {
            throw new InvalidOperationException($"OIDC token_endpoint blocked by SSRF guard: {ssrf.BlockReason}");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = challenge.RedirectUri,
            ["client_id"] = provider.ClientId,
            ["code_verifier"] = challenge.CodeVerifier
        };

        if (!string.IsNullOrWhiteSpace(provider.EncryptedClientSecret))
        {
            string decryptedSecret = _secretProtector.Unprotect(provider.EncryptedClientSecret, SecretPurposes.AuthProviders);
            if (!string.IsNullOrEmpty(decryptedSecret))
            {
                form["client_secret"] = decryptedSecret;
            }
        }

        var client = _httpClientFactory.CreateClient(OidcHttpClientNames.OidcProvider);
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
        {
            Content = new FormUrlEncodedContent(form)
        };
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OIDC token endpoint returned HTTP {(int)response.StatusCode}.");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<OidcTokenEndpointResponse>(json, JsonOptions);
    }

    private async Task<ClaimsPrincipal> ValidateIdTokenAsync(
        AuthProvider provider,
        OidcDiscoveryDocument discovery,
        string idToken,
        string expectedNonce,
        CancellationToken ct)
    {
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var jwks = await _discoveryCache.GetJwksAsync(provider.Authority, forceRefresh: false, ct);
        ClaimsPrincipal principal;

        try
        {
            principal = ValidateTokenSignatureAndClaims(handler, idToken, discovery.Issuer, provider.ClientId, jwks);
        }
        catch (SecurityTokenException ex) when (IsKeyRotationException(ex))
        {
            // Automatic 1x JWKS cache refresh to handle IdP key rotation
            _logger.LogInformation(
                "OIDC signing key mismatch for provider {ProviderId}; refreshing JWKS cache once.",
                provider.Id);
            _discoveryCache.Invalidate(provider.Authority);
            var refreshedJwks = await _discoveryCache.GetJwksAsync(provider.Authority, forceRefresh: true, ct);
            principal = ValidateTokenSignatureAndClaims(handler, idToken, discovery.Issuer, provider.ClientId, refreshedJwks);
        }

        // Verify nonce (constant-time comparison)
        string? actualNonce = principal.FindFirstValue(JwtRegisteredClaimNames.Nonce);
        if (string.IsNullOrEmpty(actualNonce) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(actualNonce),
                Encoding.UTF8.GetBytes(expectedNonce)))
        {
            throw new SecurityTokenValidationException("OIDC id_token nonce mismatch.");
        }

        return principal;
    }

    private static ClaimsPrincipal ValidateTokenSignatureAndClaims(
        JwtSecurityTokenHandler handler,
        string idToken,
        string expectedIssuer,
        string expectedAudience,
        JsonWebKeySet jwks)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = expectedIssuer,
            ValidateAudience = true,
            ValidAudience = expectedAudience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        return handler.ValidateToken(idToken, validationParameters, out _);
    }

    private static bool IsKeyRotationException(SecurityTokenException ex) =>
        ex is SecurityTokenSignatureKeyNotFoundException
        or SecurityTokenInvalidSignatureException;

    private async Task<(bool Success, SystemUser? User, int StatusCode, string? ErrorCode, string? ErrorMessage)> ResolveOrProvisionUserAsync(
        AuthProvider provider,
        string sub,
        string? email,
        bool emailVerified,
        string displayName,
        IReadOnlyList<string> externalGroups,
        CancellationToken ct)
    {
        // 1. Primary lookup by immutable (ProviderId, ExternalSubject)
        var existingLink = await _externalLogins.GetByProviderAndSubjectAsync(provider.Id, sub, ct);
        if (existingLink != null)
        {
            var linkedUser = await _users.GetByIdAsync(existingLink.UserId, ct);
            if (linkedUser == null)
            {
                return (false, null, 403, "USER_NOT_FOUND", "Linked local user account no longer exists.");
            }

            existingLink.TouchLogin(email);
            await _externalLogins.UpdateAsync(existingLink, ct);

            await ApplyMappedRoleIfMatchedAsync(provider, linkedUser, externalGroups, ct);
            return (true, linkedUser, 200, null, null);
        }

        // 2. Secondary lookup by email (strictly guarded by email_verified OR AllowEmailMatching)
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingByEmail = await _users.GetByEmailAsync(email, ct);
            if (existingByEmail != null)
            {
                if (!emailVerified && !provider.AllowEmailMatching)
                {
                    _logger.LogWarning(
                        "Blocked unverified email linking attempt on provider {ProviderId} for an existing account.",
                        provider.Id);
                    return (
                        false,
                        null,
                        403,
                        "UNVERIFIED_EMAIL_MATCH_BLOCKED",
                        "Email address is not verified by the identity provider and AllowEmailMatching is disabled.");
                }

                await _unitOfWork.ExecuteInTransactionAsync(async token =>
                {
                    await _externalLogins.AddAsync(new ExternalUserLogin(existingByEmail.Id, provider.Id, sub, email), token);
                    await ApplyMappedRoleIfMatchedAsync(provider, existingByEmail, externalGroups, token);
                    return true;
                }, ct);

                return (true, existingByEmail, 200, null, null);
            }
        }

        // 3. Unknown user -> JIT provisioning gate
        if (!provider.JitProvisioningEnabled)
        {
            return (
                false,
                null,
                403,
                "USER_NOT_PROVISIONED",
                "User is not provisioned and JIT provisioning is disabled for this identity provider.");
        }

        string effectiveEmail = !string.IsNullOrWhiteSpace(email)
            ? email
            : $"{sub}@{provider.Id}.external.local".ToLowerInvariant();
        string effectiveRole = OidcRoleMapper.ResolveEffectiveRole(
            provider.RoleMappingJson,
            externalGroups,
            provider.DefaultRole,
            provider.AllowAdminRoleMapping);

        // Assign an unguessable random PBKDF2 password hash so local password login is impossible
        // and DatabaseInitializer never treats the account as an unhashed legacy seed user.
        string randomUnusableHash = PasswordHasher.HashPassword(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

        var newUser = new SystemUser(
            id: $"usr-{Guid.NewGuid():N}",
            name: displayName,
            email: effectiveEmail,
            role: effectiveRole,
            group: "SSO",
            status: "Active",
            avatarUrl: "/avatars/user-2.svg",
            passwordHash: randomUnusableHash);

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _users.AddAsync(newUser, token);
            await _externalLogins.AddAsync(new ExternalUserLogin(newUser.Id, provider.Id, sub, effectiveEmail), token);
            return true;
        }, ct);

        return (true, newUser, 200, null, null);
    }

    private async Task ApplyMappedRoleIfMatchedAsync(
        AuthProvider provider,
        SystemUser user,
        IReadOnlyList<string> externalGroups,
        CancellationToken ct)
    {
        var mappedRole = OidcRoleMapper.ResolveMappedRole(
            provider.RoleMappingJson,
            externalGroups,
            provider.AllowAdminRoleMapping);

        if (!string.IsNullOrWhiteSpace(mappedRole) &&
            !string.Equals(user.Role, mappedRole, StringComparison.Ordinal))
        {
            user.UpdateProfile(user.Name, mappedRole, user.Group, user.Status);
            await _users.UpdateAsync(user, ct);
        }
    }

    private static bool IsEmailVerified(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("email_verified")?.Value;
        return string.Equals(claim, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractExternalGroupsAndRoles(ClaimsPrincipal principal)
    {
        var results = new List<string>();
        foreach (var claim in principal.Claims)
        {
            if (string.Equals(claim.Type, "groups", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(claim.Value))
                {
                    results.Add(claim.Value.Trim());
                }
            }
        }

        return results;
    }

    private string ResolveFrontendBaseUrl()
    {
        var configured = _configuration["Auth:FrontendBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim().TrimEnd('/');
        }

        var origins = _configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins != null && origins.Length > 0 && !string.IsNullOrWhiteSpace(origins[0]))
        {
            return origins[0].Trim().TrimEnd('/');
        }

        return string.Empty;
    }

    private sealed class OidcTokenEndpointResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }
    }
}
