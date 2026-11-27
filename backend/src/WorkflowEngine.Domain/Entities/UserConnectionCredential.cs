namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Per-user encrypted credential stored in the personal vault for connections with AuthMode = 'user_sso_vault'.
/// </summary>
public class UserConnectionCredential
{
    public string Id { get; private set; } = default!;
    public string UserId { get; private set; } = default!;
    public string ConnectionId { get; private set; } = default!;
    public string EncryptedSecretJson { get; private set; } = default!;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public Connection Connection { get; private set; } = default!;

    private UserConnectionCredential() { } // EF Core

    public UserConnectionCredential(
        string userId,
        string connectionId,
        string encryptedSecretJson,
        string? id = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("ConnectionId is required.", nameof(connectionId));
        if (string.IsNullOrWhiteSpace(encryptedSecretJson))
            throw new ArgumentException("EncryptedSecretJson is required.", nameof(encryptedSecretJson));

        Id = string.IsNullOrWhiteSpace(id) ? $"ucred-{Guid.NewGuid():N}"[..24] : id.Trim();
        UserId = userId.Trim();
        ConnectionId = connectionId.Trim();
        EncryptedSecretJson = encryptedSecretJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateSecret(string encryptedSecretJson)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecretJson))
            throw new ArgumentException("EncryptedSecretJson is required.", nameof(encryptedSecretJson));

        EncryptedSecretJson = encryptedSecretJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
