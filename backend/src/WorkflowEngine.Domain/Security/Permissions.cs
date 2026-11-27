namespace WorkflowEngine.Domain.Security;

/// <summary>
/// Fine-grained permissions. Endpoints require a permission; roles are mapped to permissions here only.
/// </summary>
public static class Permissions
{
    public const string WorkflowsWrite = "workflows.write";
    public const string DesignsWrite = "designs.write";   // SmartForms, SmartObjects, Connections
    public const string UsersManage = "users.manage";
    public const string UsersRead = "users.read";         // full user list incl. roles/status

    public static readonly IReadOnlyList<string> All = new[]
    {
        WorkflowsWrite, DesignsWrite, UsersManage, UsersRead
    };

    private static readonly IReadOnlyDictionary<string, string[]> RoleMap =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [Roles.Administrator] = All.ToArray(),
            [Roles.BpmAdministrator] = new[] { UsersRead },
            [Roles.Designer] = new[] { WorkflowsWrite, DesignsWrite }
        };

    public static IReadOnlyList<string> ForRole(string? role) =>
        role != null && RoleMap.TryGetValue(role, out var perms) ? perms : Array.Empty<string>();

    public static bool RoleHas(string? role, string permission) => ForRole(role).Contains(permission);
}
