using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Application.Features.Users.Commands;

public record DeleteUserCommand(string Id);

/// <summary>
/// Hard delete is only allowed for accounts with no linked work. Otherwise the account must be
/// deactivated (Status = Inactive) so history and pending tasks stay consistent.
/// </summary>
public static class DeleteUserHandler
{
    public static async Task<UserOperationResult> Handle(
        DeleteUserCommand command,
        ISystemUserRepository repository,
        CancellationToken ct)
    {
        var user = await repository.GetByIdAsync(command.Id, ct);
        if (user == null)
        {
            return UserOperationResult.Fail(UserOperationStatus.NotFound, "User not found.");
        }

        if (user.Role == Roles.Administrator)
        {
            return UserOperationResult.Fail(UserOperationStatus.Forbidden,
                "The principal administrator account cannot be deleted.");
        }

        if (await repository.HasLinkedWorkAsync(user.Id, user.Email, ct))
        {
            return UserOperationResult.Fail(UserOperationStatus.Conflict,
                "This user still has pending tasks or workflow instances. Deactivate the account instead of deleting it.");
        }

        await repository.DeleteAsync(user.Id, ct);
        return UserOperationResult.Ok(new UserDto(
            user.Id, user.Name, user.Email, user.Role, user.Group, user.Status, user.AvatarUrl));
    }
}
