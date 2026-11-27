using Microsoft.Extensions.Configuration;
using WorkflowEngine.Application.Common.Interfaces;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Resolves SecretRef values from IExternalVaultClient ("vault:{key}"), IConfiguration (Secrets:{ref} or direct key),
/// and environment variables. Supports prefixes like "vault:MY_SECRET", "env:MY_SECRET", or "config:Secrets:MyApi".
/// </summary>
public class ConfigurationSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;
    private readonly IExternalVaultClient _vaultClient;

    public ConfigurationSecretProvider(IConfiguration configuration, IExternalVaultClient? vaultClient = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _vaultClient = vaultClient ?? new ConfigurationExternalVaultClient(configuration);
    }

    public async Task<string?> ResolveSecretAsync(string secretRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
        {
            return null;
        }

        string trimmed = secretRef.Trim();

        if (trimmed.StartsWith("vault:", StringComparison.OrdinalIgnoreCase))
        {
            string vaultKey = trimmed[6..].Trim();
            return await _vaultClient.GetSecretAsync(vaultKey, ct);
        }

        if (trimmed.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            string envKey = trimmed[4..].Trim();
            string? envVal = Environment.GetEnvironmentVariable(envKey)
                ?? _configuration[envKey];
            return string.IsNullOrEmpty(envVal) ? null : envVal;
        }

        if (trimmed.StartsWith("config:", StringComparison.OrdinalIgnoreCase))
        {
            string cfgKey = trimmed[7..].Trim();
            string? cfgVal = _configuration[cfgKey];
            return string.IsNullOrEmpty(cfgVal) ? null : cfgVal;
        }

        // Default resolution order:
        // 1. Vault:<secretRef> via IExternalVaultClient
        // 2. Secrets:<secretRef> in IConfiguration
        // 3. Direct <secretRef> in IConfiguration
        // 4. Direct environment variable <secretRef>
        // 5. Environment variable with ':' replaced by '__'
        string? vaultValue = await _vaultClient.GetSecretAsync(trimmed, ct);
        if (!string.IsNullOrEmpty(vaultValue))
        {
            return vaultValue;
        }

        string? value = _configuration[$"Secrets:{trimmed}"]
            ?? _configuration[trimmed]
            ?? Environment.GetEnvironmentVariable(trimmed)
            ?? Environment.GetEnvironmentVariable(trimmed.Replace(":", "__", StringComparison.Ordinal));

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
