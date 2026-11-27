using FluentValidation;
using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Application.Features.Users.Commands;

public record UpdateUserCommand(
    string Id,
    string Name,
    string Role,
    string Group,
    string Status,
    string? AvatarUrl = null);

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Group).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role)
            .Must(Roles.IsValid)
            .WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
        RuleFor(x => x.Status)
            .Must(s => s == "Active" || s == "Inactive")
            .WithMessage("Status must be 'Active' or 'Inactive'.");
    }
}

public static class UpdateUserHandler
{
    public static async Task<UserOperationResult> Handle(
        UpdateUserCommand command,
        ISystemUserRepository repository,
        CancellationToken ct)
    {
        var user = await repository.GetByIdAsync(command.Id, ct);
        if (user == null)
        {
            return UserOperationResult.Fail(UserOperationStatus.NotFound, "User not found.");
        }

        bool isPrincipal = user.Role == Roles.Administrator;
        if (isPrincipal && (command.Role != Roles.Administrator || command.Status != "Active"))
        {
            return UserOperationResult.Fail(UserOperationStatus.Forbidden,
                "The principal administrator's role cannot be changed and the account cannot be deactivated.");
        }

        if (!isPrincipal && command.Role == Roles.Administrator)
        {
            return UserOperationResult.Fail(UserOperationStatus.Invalid,
                "The 'Administrator' role is reserved for the single principal administrator.");
        }

        user.UpdateProfile(command.Name, command.Role, command.Group, command.Status, command.AvatarUrl);
        await repository.UpdateAsync(user, ct);

        return UserOperationResult.Ok(new UserDto(
            user.Id, user.Name, user.Email, user.Role, user.Group, user.Status, user.AvatarUrl));
    }
}
