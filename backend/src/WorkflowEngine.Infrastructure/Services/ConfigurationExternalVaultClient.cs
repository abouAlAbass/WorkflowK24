using Microsoft.Extensions.Configuration;
using WorkflowEngine.Application.Common.Interfaces;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// MVP implementation of <see cref="IExternalVaultClient"/> reading secrets from
/// <see cref="IConfiguration"/> under the <c>Vault:{key}</c> prefix.
/// </summary>
public sealed class ConfigurationExternalVaultClient : IExternalVaultClient
{
    public const string SectionPrefix = "Vault";

    private readonly IConfiguration _configuration;

    public ConfigurationExternalVaultClient(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<string?>(null);
        }

        string trimmed = key.Trim();
        string? value = _configuration[$"{SectionPrefix}:{trimmed}"];
        return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
    }
}
