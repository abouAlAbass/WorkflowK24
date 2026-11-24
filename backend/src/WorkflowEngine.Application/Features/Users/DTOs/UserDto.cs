namespace WorkflowEngine.Application.Features.Users.DTOs;

public record UserDto(
    string Id,
    string Name,
    string Email,
    string Role,
    string Group,
    string Status,
    string AvatarUrl
);
