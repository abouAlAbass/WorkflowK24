namespace WorkflowEngine.Application.Common.Exceptions;

/// <summary>
/// Thrown at runtime when a connection configured with AuthMode = 'user_sso_vault'
/// has no stored UserConnectionCredential for the executing user.
/// Handled gracefully by WorkflowExecutionEngine to transition the instance to Waiting
/// and create a credential-entry WorkflowTask rather than faulting the instance.
/// </summary>
public class UserCredentialRequiredException : Exception
{
    public const string ErrorCode = "USER_CREDENTIAL_REQUIRED";

    public string ConnectionId { get; }
    public string ConnectionName { get; }
    public string UserId { get; }

    public UserCredentialRequiredException(string connectionId, string connectionName, string userId)
        : base($"{ErrorCode}: User '{userId}' must configure personal credentials for connection '{connectionName}' ({connectionId}).")
    {
        ConnectionId = connectionId;
        ConnectionName = connectionName;
        UserId = userId;
    }
}

/// <summary>
/// Thrown when an authenticated actor lacks CanUse or CanManage permission on a Connection.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ConnectionAccessDeniedException : Exception
{
    public const string ErrorCode = "CONNECTION_ACCESS_DENIED";

    public string ConnectionId { get; }

    public ConnectionAccessDeniedException(string connectionId, string message)
        : base(message)
    {
        ConnectionId = connectionId;
    }
}

/// <summary>
/// Thrown when a write operation (Create, Update, Delete) is attempted on a Connection
/// configured with AllowWrite = false (read-only by default).
/// Maps to HTTP 403 Forbidden with code WRITE_NOT_ALLOWED_ON_CONNECTION.
/// </summary>
public class ConnectionWriteNotAllowedException : Exception
{
    public const string ErrorCode = "WRITE_NOT_ALLOWED_ON_CONNECTION";

    public string ConnectionId { get; }
    public string Operation { get; }

    public ConnectionWriteNotAllowedException(string connectionId, string operation, string? message = null)
        : base(message ?? $"{ErrorCode}: Write operation '{operation}' is not allowed on read-only connection '{connectionId}'.")
    {
        ConnectionId = connectionId;
        Operation = operation;
    }
}

/// <summary>
/// Thrown when a connection configured with AuthMode = 'on_behalf_of' cannot exchange a valid
/// external IdP token for the executing user (missing, expired without valid refresh token, or rejected by IdP).
/// Maps to HTTP 422 Unprocessable Entity with code EXTERNAL_REAUTH_REQUIRED.
/// </summary>
public class ExternalReauthRequiredException : Exception
{
    public const string ErrorCode = "EXTERNAL_REAUTH_REQUIRED";

    public string ConnectionId { get; }
    public string ConnectionName { get; }
    public string UserId { get; }

    public ExternalReauthRequiredException(
        string connectionId,
        string connectionName,
        string userId,
        string? message = null)
        : base(message ?? $"{ErrorCode}: User '{userId}' must re-authenticate with the external identity provider to use connection '{connectionName}' ({connectionId}).")
    {
        ConnectionId = connectionId;
        ConnectionName = connectionName;
        UserId = userId;
    }
}
