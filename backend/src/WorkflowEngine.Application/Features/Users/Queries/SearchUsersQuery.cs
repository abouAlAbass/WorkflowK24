using System.Text.Json.Serialization;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Features.Users.Queries;

/// <summary>
/// Strictly sanitized user directory projection for People Picker controls.
/// Excludes password hashes, security flags, and sensitive profile attributes.
/// </summary>
public record UserDirectoryDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("department")] string Department
);

public record SearchUsersQuery(string? Query = null, int Limit = 20);

public static class SearchUsersHandler
{
    public static async Task<IReadOnlyList<UserDirectoryDto>> Handle(
        SearchUsersQuery query,
        ISystemUserRepository repository,
        CancellationToken ct)
    {
        var users = await repository.SearchAsync(query.Query, query.Limit, ct);
        return users.Select(u => new UserDirectoryDto(
            Id: u.Id,
            DisplayName: u.Name,
            Email: u.Email,
            Department: u.Group
        )).ToList();
    }
}
