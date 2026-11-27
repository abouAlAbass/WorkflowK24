using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Result of an OAuth2 On-Behalf-Of (OBO) token exchange against an external Identity Provider.
/// </summary>
public sealed record OboTokenExchangeResult(
    string AccessToken,
    DateTime ExpiresAt,
    string? Scope = null
);

/// <summary>
/// Exchanges a user's encrypted external IdP token (refreshing via EncryptedRefreshToken when expired)
/// for a downstream target-scoped token using OAuth2 OBO
/// (grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer, requested_token_use=on_behalf_of).
/// Throws ExternalReauthRequiredException (422 EXTERNAL_REAUTH_REQUIRED) when no valid user token is available.
/// </summary>
public interface IOboTokenExchangeService
{
    Task<OboTokenExchangeResult> ExchangeForUserAsync(
        Connection connection,
        string userId,
        IEnumerable<string>? alternateUserIds = null,
        ResolvedExecutionSecret? connectionClientCredentials = null,
        string? targetScope = null,
        CancellationToken ct = default);
}
