namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a registered BPM system user with assigned organizational role and permission groups.
/// </summary>
public class SystemUser
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = "End User";
    public string Group { get; private set; } = "General";
    public string Status { get; private set; } = "Active";
    public string AvatarUrl { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SystemUser() { } // EF Core

    public SystemUser(
        string id,
        string name,
        string email,
        string role = "End User",
        string group = "General",
        string status = "Active",
        string? avatarUrl = null)
    {
        Id = !string.IsNullOrWhiteSpace(id) ? id : $"usr-{Guid.NewGuid():N}";
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Name is required", nameof(name));
        Email = !string.IsNullOrWhiteSpace(email) ? email.Trim().ToLowerInvariant() : throw new ArgumentException("Email is required", nameof(email));
        Role = !string.IsNullOrWhiteSpace(role) ? role : "End User";
        Group = !string.IsNullOrWhiteSpace(group) ? group : "General";
        Status = !string.IsNullOrWhiteSpace(status) ? status : "Active";
        AvatarUrl = avatarUrl ?? "/avatars/user-2.svg";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string name, string role, string group, string status, string? avatarUrl = null)
    {
        Name = !string.IsNullOrWhiteSpace(name) ? name : Name;
        Role = !string.IsNullOrWhiteSpace(role) ? role : Role;
        Group = !string.IsNullOrWhiteSpace(group) ? group : Group;
        Status = !string.IsNullOrWhiteSpace(status) ? status : Status;
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            AvatarUrl = avatarUrl;
        }
        UpdatedAt = DateTime.UtcNow;
    }
}
