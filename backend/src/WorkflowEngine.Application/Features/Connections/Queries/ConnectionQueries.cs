namespace WorkflowEngine.Application.Features.Connections.Queries;

public record GetConnectionsQuery(
    string ActorId = "system",
    string? ActorEmail = null,
    IReadOnlyList<string>? ActorRoles = null
);

public record GetConnectionByIdQuery(
    string Id,
    string ActorId = "system",
    string? ActorEmail = null,
    IReadOnlyList<string>? ActorRoles = null
);

public record GetConnectionPermissionsQuery(
    string ConnectionId,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record GetMyConnectionCredentialsQuery(
    string UserId,
    string? UserEmail = null
);

public record GetConnectionUsageQuery(
    string ConnectionId,
    int Limit,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);

public record GetConnectionEnvironmentsQuery(
    string ConnectionId,
    string ActorId,
    string? ActorEmail,
    IReadOnlyList<string> ActorRoles
);
