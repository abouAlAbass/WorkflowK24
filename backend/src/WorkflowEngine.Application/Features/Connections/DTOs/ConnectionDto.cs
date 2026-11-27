namespace WorkflowEngine.Application.Features.Connections.DTOs;

/// <summary>
/// Public representation of a Connection. Never exposes resolved or encrypted secret values.
/// </summary>
public record ConnectionDto(
    string Id,
    string Name,
    string BaseUrl,
    string AuthType,
    string? SecretRef,
    bool HasSecretRef,
    IReadOnlyList<string> AllowedHosts,
    IReadOnlyDictionary<string, string> DefaultHeaders,
    int Version,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ServiceType = "rest",
    string AuthMode = "static",
    string SecretSource = "internal_encrypted",
    bool HasEncryptedCredentials = false,
    string? ExternalSecretKey = null,
    bool CanUse = true,
    bool CanManage = true,
    bool AllowWrite = false,
    int MaxRows = 200,
    DateTime? SecretExpiresAt = null,
    DateTime? LastRotatedAt = null,
    string HealthStatus = "Unknown",
    DateTime? LastTestedAt = null,
    DateTime? LastUsedAt = null,
    string? LastErrorSanitized = null
);

public record ConnectionPermissionDto(
    string Id,
    string ConnectionId,
    string SubjectType,
    string SubjectId,
    bool CanUse,
    bool CanManage,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Metadata-only representation of a user's personal vault credential entry. Never returns secret values.
/// </summary>
public record UserConnectionCredentialDto(
    string ConnectionId,
    string ConnectionName,
    string ServiceType,
    string AuthMode,
    bool HasCredentials,
    DateTime? UpdatedAt
);

/// <summary>
/// Metadata-only representation of a Connection's Dev/Test/Prod environment override. Never exposes secrets.
/// </summary>
public record ConnectionEnvironmentConfigDto(
    string Id,
    string ConnectionId,
    string EnvironmentName,
    string? BaseUrlOverride,
    bool HasEncryptedCredentialsOverride,
    string? ExternalSecretKeyOverride,
    DateTime UpdatedAt
);

public record ConnectionUsageLogDto(
    string Id,
    string ConnectionId,
    string? InstanceId,
    string? WorkflowKey,
    string? NodeId,
    string ActorId,
    bool Success,
    long DurationMs,
    DateTime ExecutedAt
);

public record ConnectionUsageResponseDto(
    string ConnectionId,
    string ConnectionName,
    string HealthStatus,
    DateTime? LastTestedAt,
    DateTime? LastUsedAt,
    DateTime? LastRotatedAt,
    DateTime? SecretExpiresAt,
    string? LastErrorSanitized,
    IReadOnlyList<ConnectionUsageLogDto> Logs
);
