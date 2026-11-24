using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Users.Queries;

public record GetUsersQuery;

public static class GetUsersHandler
{
    public static async Task<IReadOnlyList<UserDto>> Handle(
        GetUsersQuery query,
        ISystemUserRepository repository,
        CancellationToken ct)
    {
        var users = await repository.GetAllAsync(ct);
        return users.Select(u => new UserDto(
            u.Id,
            u.Name,
            u.Email,
            u.Role,
            u.Group,
            u.Status,
            u.AvatarUrl
        )).ToList();
    }
}
