namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Encrypted external IdP access/refresh token store per (UserId, ProviderId).
/// Encrypted exclusively under SecretPurposes.ExternalTokens and NEVER embedded in internal Tamam JWTs.
/// </summary>
public class ExternalUserToken
{
    public string Id { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string ProviderId { get; private set; } = string.Empty;
    public string EncryptedAccessToken { get; private set; } = string.Empty;
    public string? EncryptedRefreshToken { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ExternalUserToken() { } // EF Core

    public ExternalUserToken(
        string userId,
        string providerId,
        string encryptedAccessToken,
        string? encryptedRefreshToken,
        DateTime expiresAt,
        DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("ProviderId is required.", nameof(providerId));
        if (string.IsNullOrWhiteSpace(encryptedAccessToken))
            throw new ArgumentException("EncryptedAccessToken is required.", nameof(encryptedAccessToken));

        var utcNow = now ?? DateTime.UtcNow;
        Id = $"exttok-{Guid.NewGuid():N}";
        UserId = userId.Trim();
        ProviderId = providerId.Trim();
        EncryptedAccessToken = encryptedAccessToken;
        EncryptedRefreshToken = string.IsNullOrWhiteSpace(encryptedRefreshToken) ? null : encryptedRefreshToken;
        ExpiresAt = expiresAt;
        UpdatedAt = utcNow;
    }

    public void UpdateTokens(
        string encryptedAccessToken,
        string? encryptedRefreshToken,
        DateTime expiresAt,
        bool preserveExistingRefreshTokenIfNull = true,
        DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(encryptedAccessToken))
            throw new ArgumentException("EncryptedAccessToken is required.", nameof(encryptedAccessToken));

        EncryptedAccessToken = encryptedAccessToken;
        if (!string.IsNullOrWhiteSpace(encryptedRefreshToken))
        {
            EncryptedRefreshToken = encryptedRefreshToken;
        }
        else if (!preserveExistingRefreshTokenIfNull)
        {
            EncryptedRefreshToken = null;
        }

        ExpiresAt = expiresAt;
        UpdatedAt = now ?? DateTime.UtcNow;
    }
}
