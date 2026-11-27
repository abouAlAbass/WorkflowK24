namespace WorkflowEngine.Application.Features.Users.DTOs;

public enum UserOperationStatus
{
    Ok,
    NotFound,
    Forbidden,
    Conflict,
    Invalid
}

/// <summary>Outcome of a user mutation; endpoints map it to 200/404/403/409/422.</summary>
public record UserOperationResult(UserOperationStatus Status, UserDto? User = null, string? Error = null)
{
    public static UserOperationResult Ok(UserDto? user = null) => new(UserOperationStatus.Ok, user);
    public static UserOperationResult Fail(UserOperationStatus status, string error) => new(status, null, error);
}

public record UserAuditDto(
    string Id,
    string ActorId,
    string ActorEmail,
    string Action,
    string TargetUserId,
    string TargetEmail,
    string? Details,
    DateTime OccurredAt);
