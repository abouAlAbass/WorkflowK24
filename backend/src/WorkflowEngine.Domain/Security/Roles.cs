namespace WorkflowEngine.Domain.Security;

/// <summary>
/// Single source of truth for system role names. Never hardcode role strings elsewhere.
/// </summary>
public static class Roles
{
    /// <summary>The unique principal administrator (can design workflows and manage users).</summary>
    public const string Administrator = "Administrator";
    public const string BpmAdministrator = "BPM Administrator";
    public const string ProcessManager = "Process Manager";
    /// <summary>Canonical studio designer role (alongside Admin and User) for workflows, forms, SmartObjects, and connections.</summary>
    public const string Designer = "Designer";
    public const string WorkflowDesigner = "Workflow Designer";
    public const string WorkflowDeveloper = "Workflow Developer";
    public const string EndUser = "End User";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Administrator, BpmAdministrator, ProcessManager, Designer, WorkflowDesigner, WorkflowDeveloper, EndUser
    };

    /// <summary>Roles that may be handed out through the API (Administrator is reserved).</summary>
    public static readonly IReadOnlyList<string> Assignable = All.Where(r => r != Administrator).ToArray();

    public static bool IsValid(string? role) => role != null && All.Contains(role, StringComparer.Ordinal);
}
