namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Resolves external secret references (never stored in plaintext in workflow definitions or DB)
/// from secure configuration or environment variables.
/// </summary>
public interface ISecretProvider
{
    /// <summary>
    /// Resolves a secret value for the given reference key, or returns null if not found.
    /// </summary>
    Task<string?> ResolveSecretAsync(string secretRef, CancellationToken ct = default);
}
