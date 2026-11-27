using FluentValidation;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Features.Connections.Commands;

public record CreateConnectionCommand(
    string? Id,
    string Name,
    string? BaseUrl,
    string AuthType,
    string? SecretRef,
    List<string>? AllowedHosts,
    Dictionary<string, string>? DefaultHeaders,
    string ActorId,
    string ServiceType = "rest",
    string AuthMode = "static",
    string SecretSource = "internal_encrypted",
    string? CredentialsJson = null,
    string? ExternalSecretKey = null,
    bool AllowWrite = false,
    int MaxRows = Connection.DefaultMaxRows,
    DateTime? SecretExpiresAt = null
);

public record UpdateConnectionCommand(
    string Id,
    string Name,
    string? BaseUrl,
    string AuthType,
    string? SecretRef,
    List<string>? AllowedHosts,
    Dictionary<string, string>? DefaultHeaders,
    int? ExpectedVersion,
    string ActorId,
    string? ActorEmail = null,
    IReadOnlyList<string>? ActorRoles = null,
    string? ServiceType = null,
    string? AuthMode = null,
    string? SecretSource = null,
    string? CredentialsJson = null,
    bool ClearCredentials = false,
    string? ExternalSecretKey = null,
    bool? AllowWrite = null,
    int? MaxRows = null
);

public record DeleteConnectionCommand(
    string Id,
    string ActorId,
    string? ActorEmail = null,
    IReadOnlyList<string>? ActorRoles = null
);

public record TestConnectionCommand(
    string Id,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record InvokeConnectionCommand(
    string Id,
    string Operation,
    string? TableName,
    string? SchemaName,
    string? Path,
    string? HttpMethod,
    Dictionary<string, string>? Headers,
    string? BodyJson,
    Dictionary<string, object?>? Parameters,
    int? TimeoutSeconds,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record UpsertConnectionPermissionCommand(
    string ConnectionId,
    string SubjectType,
    string SubjectId,
    bool CanUse,
    bool CanManage,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record DeleteConnectionPermissionCommand(
    string ConnectionId,
    string PermissionId,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record UpsertMyConnectionCredentialCommand(
    string UserId,
    string? UserEmail,
    string ConnectionId,
    string SecretJson
);

public record DeleteMyConnectionCredentialCommand(
    string UserId,
    string? UserEmail,
    string ConnectionId
);

public record RotateConnectionSecretCommand(
    string ConnectionId,
    string? CredentialsJson,
    string? ExternalSecretKey,
    DateTime? SecretExpiresAt,
    int? ExpectedVersion,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record UpsertConnectionEnvironmentConfigCommand(
    string ConnectionId,
    string EnvironmentName,
    string? BaseUrlOverride,
    string? CredentialsJsonOverride,
    bool ClearCredentialsOverride,
    string? ExternalSecretKeyOverride,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public class CreateConnectionCommandValidator : AbstractValidator<CreateConnectionCommand>
{
    public CreateConnectionCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Connection name is required.")
            .MaximumLength(150);

        RuleFor(x => x.ServiceType)
            .Must(t => string.IsNullOrWhiteSpace(t) || Connection.AllowedServiceTypes.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage($"ServiceType must be one of: {string.Join(", ", Connection.AllowedServiceTypes)}.");

        RuleFor(x => x.BaseUrl)
            .NotEmpty().WithMessage("BaseUrl is required for REST connections.")
            .MaximumLength(1000)
            .Must(BeValidHttpUrl).WithMessage("BaseUrl must be a valid absolute HTTP or HTTPS URL.")
            .When(x => IsRestService(x.ServiceType));

        RuleFor(x => x.BaseUrl)
            .MaximumLength(1000)
            .When(x => !IsRestService(x.ServiceType) && x.BaseUrl != null);

        RuleFor(x => x.AuthType)
            .Must(t => string.IsNullOrWhiteSpace(t) || Connection.AllowedAuthTypes.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage($"AuthType must be one of: {string.Join(", ", Connection.AllowedAuthTypes)}.");

        RuleFor(x => x.AuthMode)
            .Must(m => string.IsNullOrWhiteSpace(m) || Connection.AllowedAuthModes.Contains(m.Trim().ToLowerInvariant()))
            .WithMessage($"AuthMode must be one of: {string.Join(", ", Connection.AllowedAuthModes)}.");

        RuleFor(x => x.SecretSource)
            .Must(s => string.IsNullOrWhiteSpace(s) || Connection.AllowedSecretSources.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage($"SecretSource must be one of: {string.Join(", ", Connection.AllowedSecretSources)}.");

        RuleFor(x => x.SecretRef)
            .MaximumLength(500)
            .When(x => x.SecretRef != null);

        RuleFor(x => x.MaxRows)
            .InclusiveBetween(Connection.MinMaxRows, Connection.MaxAllowedRows)
            .WithMessage($"MaxRows must be between {Connection.MinMaxRows} and {Connection.MaxAllowedRows}.");

        RuleFor(x => x.SecretRef)
            .NotEmpty()
            .When(x =>
                IsRestService(x.ServiceType) &&
                !string.Equals(x.AuthType?.Trim(), "none", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.AuthMode?.Trim(), "user_sso_vault", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(x.AuthMode?.Trim(), "on_behalf_of", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(x.CredentialsJson) &&
                string.IsNullOrWhiteSpace(x.ExternalSecretKey))
            .WithMessage("SecretRef, CredentialsJson, or ExternalSecretKey is required when AuthType is not 'none'.");
    }

    private static bool IsRestService(string? serviceType) =>
        string.IsNullOrWhiteSpace(serviceType) ||
        string.Equals(serviceType.Trim(), "rest", StringComparison.OrdinalIgnoreCase);

    private static bool BeValidHttpUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

public class UpdateConnectionCommandValidator : AbstractValidator<UpdateConnectionCommand>
{
    public UpdateConnectionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Connection name is required.")
            .MaximumLength(150);

        RuleFor(x => x.ServiceType)
            .Must(t => string.IsNullOrWhiteSpace(t) || Connection.AllowedServiceTypes.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage($"ServiceType must be one of: {string.Join(", ", Connection.AllowedServiceTypes)}.");

        RuleFor(x => x.BaseUrl)
            .NotEmpty().WithMessage("BaseUrl is required for REST connections.")
            .MaximumLength(1000)
            .Must(BeValidHttpUrl).WithMessage("BaseUrl must be a valid absolute HTTP or HTTPS URL.")
            .When(x => IsRestService(x.ServiceType));

        RuleFor(x => x.AuthType)
            .Must(t => string.IsNullOrWhiteSpace(t) || Connection.AllowedAuthTypes.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage($"AuthType must be one of: {string.Join(", ", Connection.AllowedAuthTypes)}.");

        RuleFor(x => x.AuthMode)
            .Must(m => string.IsNullOrWhiteSpace(m) || Connection.AllowedAuthModes.Contains(m.Trim().ToLowerInvariant()))
            .WithMessage($"AuthMode must be one of: {string.Join(", ", Connection.AllowedAuthModes)}.");

        RuleFor(x => x.SecretSource)
            .Must(s => string.IsNullOrWhiteSpace(s) || Connection.AllowedSecretSources.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage($"SecretSource must be one of: {string.Join(", ", Connection.AllowedSecretSources)}.");

        RuleFor(x => x.SecretRef)
            .MaximumLength(500)
            .When(x => x.SecretRef != null);

        RuleFor(x => x.MaxRows)
            .InclusiveBetween(Connection.MinMaxRows, Connection.MaxAllowedRows)
            .When(x => x.MaxRows.HasValue)
            .WithMessage($"MaxRows must be between {Connection.MinMaxRows} and {Connection.MaxAllowedRows}.");
    }

    private static bool IsRestService(string? serviceType) =>
        string.IsNullOrWhiteSpace(serviceType) ||
        string.Equals(serviceType.Trim(), "rest", StringComparison.OrdinalIgnoreCase);

    private static bool BeValidHttpUrl(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) &&
               Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
