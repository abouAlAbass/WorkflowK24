using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Application.Services.Connections;

public interface IConnectionAccessService
{
    bool IsAdmin(IEnumerable<string> roles);
    Task<bool> CanUseAsync(Connection connection, string actorId, string? actorEmail, IEnumerable<string> roles, CancellationToken ct = default);
    Task<bool> CanManageAsync(Connection connection, string actorId, string? actorEmail, IEnumerable<string> roles, CancellationToken ct = default);
    Task<IReadOnlyList<(Connection Connection, bool CanUse, bool CanManage)>> FilterAccessibleAsync(
        IReadOnlyList<Connection> connections,
        string actorId,
        string? actorEmail,
        IEnumerable<string> roles,
        CancellationToken ct = default);
}

public class ConnectionAccessService : IConnectionAccessService
{
    private readonly IConnectionPermissionRepository _permissionRepository;

    public ConnectionAccessService(IConnectionPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
    }

    public bool IsAdmin(IEnumerable<string> roles)
    {
        return roles.Any(r =>
            string.Equals(r, Roles.Administrator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, Roles.BpmAdministrator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> CanUseAsync(
        Connection connection,
        string actorId,
        string? actorEmail,
        IEnumerable<string> roles,
        CancellationToken ct = default)
    {
        var roleList = roles.ToList();
        if (IsAdmin(roleList) || IsCreator(connection, actorId, actorEmail))
        {
            return true;
        }

        var perms = await _permissionRepository.GetByConnectionIdAsync(connection.Id, ct);
        return EvaluateCanUse(perms, actorId, actorEmail, roleList);
    }

    public async Task<bool> CanManageAsync(
        Connection connection,
        string actorId,
        string? actorEmail,
        IEnumerable<string> roles,
        CancellationToken ct = default)
    {
        var roleList = roles.ToList();
        if (IsAdmin(roleList) || IsCreator(connection, actorId, actorEmail))
        {
            return true;
        }

        var perms = await _permissionRepository.GetByConnectionIdAsync(connection.Id, ct);
        return EvaluateCanManage(perms, actorId, actorEmail, roleList);
    }

    public async Task<IReadOnlyList<(Connection Connection, bool CanUse, bool CanManage)>> FilterAccessibleAsync(
        IReadOnlyList<Connection> connections,
        string actorId,
        string? actorEmail,
        IEnumerable<string> roles,
        CancellationToken ct = default)
    {
        var roleList = roles.ToList();
        if (IsAdmin(roleList))
        {
            return connections.Select(c => (c, true, true)).ToList();
        }

        var allPermissions = await _permissionRepository.GetAllAsync(ct);
        var byConnection = allPermissions
            .GroupBy(p => p.ConnectionId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ConnectionPermission>)g.ToList(), StringComparer.Ordinal);

        var result = new List<(Connection, bool, bool)>();
        foreach (var conn in connections)
        {
            if (IsCreator(conn, actorId, actorEmail))
            {
                result.Add((conn, true, true));
                continue;
            }

            byConnection.TryGetValue(conn.Id, out var perms);
            perms ??= Array.Empty<ConnectionPermission>();

            bool canManage = EvaluateCanManage(perms, actorId, actorEmail, roleList);
            bool canUse = canManage || EvaluateCanUse(perms, actorId, actorEmail, roleList);
            if (canUse)
            {
                result.Add((conn, true, canManage));
            }
        }

        return result;
    }

    private static bool IsCreator(Connection connection, string actorId, string? actorEmail)
    {
        if (string.IsNullOrWhiteSpace(connection.CreatedBy))
        {
            return false;
        }

        return (!string.IsNullOrWhiteSpace(actorId) &&
                string.Equals(connection.CreatedBy, actorId, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(actorEmail) &&
                string.Equals(connection.CreatedBy, actorEmail, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EvaluateCanUse(
        IReadOnlyList<ConnectionPermission> perms,
        string actorId,
        string? actorEmail,
        IReadOnlyList<string> roles)
    {
        return perms.Any(p => (p.CanUse || p.CanManage) && MatchesSubject(p, actorId, actorEmail, roles));
    }

    private static bool EvaluateCanManage(
        IReadOnlyList<ConnectionPermission> perms,
        string actorId,
        string? actorEmail,
        IReadOnlyList<string> roles)
    {
        return perms.Any(p => p.CanManage && MatchesSubject(p, actorId, actorEmail, roles));
    }

    private static bool MatchesSubject(
        ConnectionPermission perm,
        string actorId,
        string? actorEmail,
        IReadOnlyList<string> roles)
    {
        if (string.Equals(perm.SubjectType, "User", StringComparison.OrdinalIgnoreCase))
        {
            return (!string.IsNullOrWhiteSpace(actorId) &&
                    string.Equals(perm.SubjectId, actorId, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrWhiteSpace(actorEmail) &&
                    string.Equals(perm.SubjectId, actorEmail, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(perm.SubjectType, "Role", StringComparison.OrdinalIgnoreCase))
        {
            return roles.Any(r => string.Equals(r, perm.SubjectId, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}
