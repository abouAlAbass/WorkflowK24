using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Application.Common.Interfaces;

/// <summary>
/// Decrypted runtime credential payload resolved strictly at execution time (never returned by read APIs).
/// </summary>
public sealed class ResolvedExecutionSecret
{
    public string RawValue { get; }
    public IReadOnlyDictionary<string, string> Fields { get; }
    public IReadOnlyList<string> SecretsToRedact { get; }
    public string? EffectiveBaseUrl { get; }
    public string? ActiveEnvironment { get; }

    public ResolvedExecutionSecret(
        string rawValue,
        IReadOnlyDictionary<string, string> fields,
        IReadOnlyList<string> secretsToRedact,
        string? effectiveBaseUrl = null,
        string? activeEnvironment = null)
    {
        RawValue = rawValue;
        Fields = fields;
        SecretsToRedact = secretsToRedact;
        EffectiveBaseUrl = effectiveBaseUrl;
        ActiveEnvironment = activeEnvironment;
    }

    public string? GetField(params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (Fields.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }

        return null;
    }
}

/// <summary>
/// Resolves connection secrets and active environment overrides (TAMAM_ENVIRONMENT: Dev / Test / Prod)
/// exclusively during runtime execution or connection diagnostics.
/// Supports internal Data Protection encryption, external vaults, user personal vaults (user_sso_vault),
/// OAuth2 On-Behalf-Of (on_behalf_of), and legacy SecretRef fallback for 100% REST backward compatibility.
/// </summary>
public interface IExecutionSecretResolver
{
    string ActiveEnvironment => "Dev";

    Task<ResolvedExecutionSecret?> ResolveAsync(
        Connection connection,
        string? userId = null,
        IEnumerable<string>? alternateUserIds = null,
        CancellationToken ct = default);

    Task<string> ResolveEffectiveBaseUrlAsync(
        Connection connection,
        CancellationToken ct = default) => Task.FromResult(connection.BaseUrl);
}
