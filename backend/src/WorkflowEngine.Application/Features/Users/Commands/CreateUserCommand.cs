using FluentValidation;
using WorkflowEngine.Application.Features.Users.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Application.Features.Users.Commands;

public record CreateUserCommand(
    string Name,
    string Email,
    string Role = "End User",
    string Group = "General",
    string Status = "Active",
    string? AvatarUrl = null,
    string? Password = null
);

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Group).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password)
            .MinimumLength(8)
            .When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}

public static class CreateUserHandler
{
    public static async Task<UserDto> Handle(
        CreateUserCommand command,
        ISystemUserRepository repository,
        CancellationToken ct)
    {
        var existing = await repository.GetByEmailAsync(command.Email, ct);
        if (existing != null)
        {
            throw new InvalidOperationException($"User with email '{command.Email}' already exists.");
        }

        string passwordHash = !string.IsNullOrWhiteSpace(command.Password)
            ? PasswordHasher.HashPassword(command.Password)
            : string.Empty;

        var user = new SystemUser(
            id: $"usr-{Guid.NewGuid():N}",
            name: command.Name,
            email: command.Email,
            role: command.Role,
            group: command.Group,
            status: command.Status,
            avatarUrl: command.AvatarUrl,
            passwordHash: passwordHash
        );

        var created = await repository.AddAsync(user, ct);

        return new UserDto(
            created.Id,
            created.Name,
            created.Email,
            created.Role,
            created.Group,
            created.Status,
            created.AvatarUrl
        );
    }
}
