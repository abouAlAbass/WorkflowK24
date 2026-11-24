using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Users.Commands;

public record DeleteUserCommand(string Id);

public static class DeleteUserHandler
{
    public static async Task<bool> Handle(
        DeleteUserCommand command,
        ISystemUserRepository repository,
        CancellationToken ct)
    {
        return await repository.DeleteAsync(command.Id, ct);
    }
}
