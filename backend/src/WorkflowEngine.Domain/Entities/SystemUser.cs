using WorkflowEngine.Domain.Security;

namespace WorkflowEngine.Domain.Entities;

/// <summary>
/// Represents a registered BPM system user with assigned organizational role and permission groups.
/// </summary>
public class SystemUser
{
    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Role { get; private set; } = "End User";
    public string Group { get; private set; } = "General";
    public string Status { get; private set; } = "Active";
    public string AvatarUrl { get; private set; } = string.Empty;
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEnd { get; private set; }
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
        string? avatarUrl = null,
        string? passwordHash = null)
    {
        Id = !string.IsNullOrWhiteSpace(id) ? id : $"usr-{Guid.NewGuid():N}";
        Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentException("Name is required", nameof(name));
        Email = !string.IsNullOrWhiteSpace(email) ? email.Trim().ToLowerInvariant() : throw new ArgumentException("Email is required", nameof(email));
        Role = !string.IsNullOrWhiteSpace(role) ? role : "End User";
        Group = !string.IsNullOrWhiteSpace(group) ? group : "General";
        Status = !string.IsNullOrWhiteSpace(status) ? status : "Active";
        AvatarUrl = avatarUrl ?? "/avatars/user-2.svg";
        PasswordHash = passwordHash ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPassword(string plainTextPassword)
    {
        PasswordHash = PasswordHasher.HashPassword(plainTextPassword);
        FailedLoginCount = 0;
        LockoutEnd = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEnd.HasValue && LockoutEnd.Value > utcNow;

    /// <summary>Records a failed sign-in; returns true when this failure triggers a lockout.</summary>
    public bool RegisterFailedLogin(int maxAttempts, TimeSpan lockoutDuration, DateTime utcNow)
    {
        // An expired lockout starts a fresh window.
        if (LockoutEnd.HasValue && LockoutEnd.Value <= utcNow)
        {
            FailedLoginCount = 0;
            LockoutEnd = null;
        }

        FailedLoginCount++;
        UpdatedAt = utcNow;
        if (FailedLoginCount >= maxAttempts)
        {
            LockoutEnd = utcNow.Add(lockoutDuration);
            return true;
        }

        return false;
    }

    public void ResetLockout()
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool VerifyPassword(string plainTextPassword)
    {
        return PasswordHasher.VerifyPassword(plainTextPassword, PasswordHash);
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
