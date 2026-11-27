namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Abstraction over an external secret vault (MVP backed by IConfiguration "Vault:*" prefix;
/// extensible to Azure Key Vault, HashiCorp Vault, or AWS Secrets Manager).
/// </summary>
public interface IExternalVaultClient
{
    /// <summary>
    /// Retrieves a secret value by key from the configured external vault, or null if not found.
    /// </summary>
    Task<string?> GetSecretAsync(string key, CancellationToken ct = default);
}
