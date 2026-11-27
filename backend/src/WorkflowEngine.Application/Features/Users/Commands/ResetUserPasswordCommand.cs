using FluentValidation;
using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Application.Features.Users.Commands;

public record ResetUserPasswordCommand(string Id, string NewPassword);

public class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewPassword)
            .Must(PasswordPolicy.IsValid)
            .WithMessage(PasswordPolicy.Description);
    }
}

public static class ResetUserPasswordHandler
{
    public static async Task<UserOperationResult> Handle(
        ResetUserPasswordCommand command,
        ISystemUserRepository repository,
        CancellationToken ct)
    {
        var user = await repository.GetByIdAsync(command.Id, ct);
        if (user == null)
        {
            return UserOperationResult.Fail(UserOperationStatus.NotFound, "User not found.");
        }

        user.SetPassword(command.NewPassword); // also clears any lockout
        await repository.UpdateAsync(user, ct);

        return UserOperationResult.Ok(new UserDto(
            user.Id, user.Name, user.Email, user.Role, user.Group, user.Status, user.AvatarUrl));
    }
}
