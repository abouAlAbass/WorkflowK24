namespace WorkflowEngine.Domain.Security;

/// <summary>Password complexity rules shared by user creation, reset and change-password.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public const string Description =
        "Password must be at least 8 characters and contain an uppercase letter, a lowercase letter and a digit.";

    public static bool IsValid(string? password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= MinLength
        && password.Any(char.IsUpper)
        && password.Any(char.IsLower)
        && password.Any(char.IsDigit);
}
