namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Canonical Data Protection purpose strings isolating encrypted secrets by domain boundary.
/// A secret protected under one purpose can never be unprotected under a different purpose.
/// </summary>
public static class SecretPurposes
{
    public const string AuthProviders = "Tamam.AuthProviders.Secrets";
    public const string Connections = "Tamam.Connections.Secrets";
    public const string UserCredentials = "Tamam.UserCredentials.Secrets";
    public const string ExternalTokens = "Tamam.ExternalTokens.Secrets";

    public static readonly IReadOnlyList<string> All = new[]
    {
        AuthProviders,
        Connections,
        UserCredentials,
        ExternalTokens
    };

    public static bool IsValid(string? purpose) =>
        !string.IsNullOrWhiteSpace(purpose) && All.Contains(purpose, StringComparer.Ordinal);
}

/// <summary>
/// Encrypts and decrypts application secrets using purpose-isolated, versioned envelopes
/// backed by the persisted ASP.NET Core Data Protection key ring.
/// </summary>
public interface ISecretProtector
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> under the given <paramref name="purpose"/>
    /// and returns a versioned JSON envelope (e.g. <c>{"v":1,"cipher":"..."}</c>).
    /// </summary>
    string Protect(string plaintext, string purpose);

    /// <summary>
    /// Decrypts a versioned JSON envelope produced by <see cref="Protect"/> using the same <paramref name="purpose"/>.
    /// Throws <see cref="System.Security.Cryptography.CryptographicException"/> if the purpose differs or payload is invalid.
    /// </summary>
    string Unprotect(string protectedPayload, string purpose);
}
