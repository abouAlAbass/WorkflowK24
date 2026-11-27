namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Links a local SystemUser to an external OIDC identity via the immutable (ProviderId, ExternalSubject) pair.
/// </summary>
public class ExternalUserLogin
{
    public string Id { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string ProviderId { get; private set; } = string.Empty;
    public string ExternalSubject { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime LastLoginAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ExternalUserLogin() { } // EF Core

    public ExternalUserLogin(
        string userId,
        string providerId,
        string externalSubject,
        string email,
        DateTime? now = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("ProviderId is required.", nameof(providerId));
        if (string.IsNullOrWhiteSpace(externalSubject))
            throw new ArgumentException("ExternalSubject (sub) is required.", nameof(externalSubject));

        var utcNow = now ?? DateTime.UtcNow;
        Id = $"extl-{Guid.NewGuid():N}";
        UserId = userId.Trim();
        ProviderId = providerId.Trim();
        ExternalSubject = externalSubject.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        LastLoginAt = utcNow;
        CreatedAt = utcNow;
    }

    public void TouchLogin(string? email = null, DateTime? now = null)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = email.Trim().ToLowerInvariant();
        }

        LastLoginAt = now ?? DateTime.UtcNow;
    }
}
