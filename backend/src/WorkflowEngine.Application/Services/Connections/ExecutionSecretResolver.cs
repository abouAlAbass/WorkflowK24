using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WorkflowEngine.Application.Common.Exceptions;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Services.Connections;

/// <summary>
/// Runtime-only secret &amp; environment resolver supporting:
/// - Environment overrides via ConnectionEnvironmentConfig matching TAMAM_ENVIRONMENT (default 'Dev')
/// - AuthMode = 'on_behalf_of': exchanges the user's encrypted ExternalUserToken via IOboTokenExchangeService
/// - AuthMode = 'user_sso_vault': resolves per-user UserConnectionCredential (throws UserCredentialRequiredException if absent)
/// - SecretSource = 'external_vault': resolves via IExternalVaultClient
/// - SecretSource = 'internal_encrypted': decrypts EncryptedCredentialsJson via ISecretProtector
/// - Legacy fallback: resolves Connection.SecretRef via ISecretProvider for 100% REST backward compatibility.
/// </summary>
public class ExecutionSecretResolver : IExecutionSecretResolver
{
    private readonly ISecretProtector _secretProtector;
    private readonly IExternalVaultClient _externalVaultClient;
    private readonly ISecretProvider _secretProvider;
    private readonly IUserConnectionCredentialRepository _userCredentialRepository;
    private readonly ISystemUserRepository? _userRepository;
    private readonly IConnectionEnvironmentConfigRepository? _environmentConfigRepository;
    private readonly IOboTokenExchangeService? _oboTokenExchangeService;
    private readonly IConfiguration? _configuration;

    public ExecutionSecretResolver(
        ISecretProtector secretProtector,
        IExternalVaultClient externalVaultClient,
        ISecretProvider secretProvider,
        IUserConnectionCredentialRepository userCredentialRepository,
        ISystemUserRepository? userRepository = null,
        IConnectionEnvironmentConfigRepository? environmentConfigRepository = null,
        IOboTokenExchangeService? oboTokenExchangeService = null,
        IConfiguration? configuration = null)
    {
        _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
        _externalVaultClient = externalVaultClient ?? throw new ArgumentNullException(nameof(externalVaultClient));
        _secretProvider = secretProvider ?? throw new ArgumentNullException(nameof(secretProvider));
        _userCredentialRepository = userCredentialRepository ?? throw new ArgumentNullException(nameof(userCredentialRepository));
        _userRepository = userRepository;
        _environmentConfigRepository = environmentConfigRepository;
        _oboTokenExchangeService = oboTokenExchangeService;
        _configuration = configuration;
    }

    public string ActiveEnvironment
    {
        get
        {
            string? raw = _configuration?["TAMAM_ENVIRONMENT"]
                ?? Environment.GetEnvironmentVariable("TAMAM_ENVIRONMENT");

            if (!string.IsNullOrWhiteSpace(raw) &&
                ConnectionEnvironmentConfig.CanonicalEnvironmentNames.TryGetValue(raw.Trim(), out var canonical))
            {
                return canonical;
            }

            return "Dev";
        }
    }

    public async Task<string> ResolveEffectiveBaseUrlAsync(Connection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var envConfig = await GetActiveEnvironmentConfigAsync(connection.Id, ct);
        return !string.IsNullOrWhiteSpace(envConfig?.BaseUrlOverride)
            ? envConfig.BaseUrlOverride!
            : connection.BaseUrl;
    }

    public async Task<ResolvedExecutionSecret?> ResolveAsync(
        Connection connection,
        string? userId = null,
        IEnumerable<string>? alternateUserIds = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        string activeEnv = ActiveEnvironment;
        var envConfig = await GetActiveEnvironmentConfigAsync(connection.Id, ct);
        string effectiveBaseUrl = !string.IsNullOrWhiteSpace(envConfig?.BaseUrlOverride)
            ? envConfig.BaseUrlOverride!
            : connection.BaseUrl;

        // 1. OAuth2 On-Behalf-Of (OBO) mode
        if (string.Equals(connection.AuthMode, "on_behalf_of", StringComparison.OrdinalIgnoreCase))
        {
            if (_oboTokenExchangeService == null)
            {
                throw new ExternalReauthRequiredException(
                    connection.Id,
                    connection.Name,
                    userId ?? "unknown",
                    $"{ExternalReauthRequiredException.ErrorCode}: OBO token exchange service is not configured.");
            }

            var clientCredentials = await ResolveConnectionSecretAsync(connection, envConfig, effectiveBaseUrl, activeEnv, ct);
            var oboResult = await _oboTokenExchangeService.ExchangeForUserAsync(
                connection,
                userId ?? string.Empty,
                alternateUserIds,
                clientCredentials,
                targetScope: clientCredentials?.GetField("scope"),
                ct: ct);

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["access_token"] = oboResult.AccessToken,
                ["token"] = oboResult.AccessToken
            };
            var secretsToRedact = new List<string> { oboResult.AccessToken };
            if (clientCredentials != null)
            {
                secretsToRedact.AddRange(clientCredentials.SecretsToRedact);
            }

            return new ResolvedExecutionSecret(
                rawValue: oboResult.AccessToken,
                fields: fields,
                secretsToRedact: secretsToRedact.Distinct(StringComparer.Ordinal).ToList(),
                effectiveBaseUrl: effectiveBaseUrl,
                activeEnvironment: activeEnv);
        }

        // 2. Per-user SSO personal vault mode
        if (string.Equals(connection.AuthMode, "user_sso_vault", StringComparison.OrdinalIgnoreCase))
        {
            var candidateIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                candidateIds.Add(userId.Trim());
            }

            if (alternateUserIds != null)
            {
                foreach (var alt in alternateUserIds)
                {
                    if (!string.IsNullOrWhiteSpace(alt))
                    {
                        candidateIds.Add(alt.Trim());
                    }
                }
            }

            // Enrich candidate IDs with both SystemUser.Id and SystemUser.Email so InitiatorId (email or sub) always matches personal vault records
            if (_userRepository != null && candidateIds.Count > 0)
            {
                string primary = candidateIds[0];
                var user = await _userRepository.GetByIdAsync(primary, ct)
                    ?? await _userRepository.GetByEmailAsync(primary, ct);
                if (user != null)
                {
                    if (!string.IsNullOrWhiteSpace(user.Id)) candidateIds.Add(user.Id);
                    if (!string.IsNullOrWhiteSpace(user.Email)) candidateIds.Add(user.Email);
                }
            }

            string effectiveUserId = candidateIds.FirstOrDefault() ?? "unknown";
            var userCred = candidateIds.Count > 0
                ? await _userCredentialRepository.GetByAnyUserIdAndConnectionAsync(candidateIds, connection.Id, ct)
                : null;

            if (userCred == null || string.IsNullOrWhiteSpace(userCred.EncryptedSecretJson))
            {
                throw new UserCredentialRequiredException(connection.Id, connection.Name, effectiveUserId);
            }

            string plaintext = _secretProtector.Unprotect(userCred.EncryptedSecretJson, SecretPurposes.UserCredentials);
            return BuildResolvedSecret(plaintext, effectiveBaseUrl, activeEnv);
        }

        // 3. Connection-level secret (with environment override precedence)
        var resolved = await ResolveConnectionSecretAsync(connection, envConfig, effectiveBaseUrl, activeEnv, ct);
        if (resolved != null)
        {
            return resolved;
        }

        // Even without credentials, propagate BaseUrlOverride when configured for the active environment
        if (!string.IsNullOrWhiteSpace(envConfig?.BaseUrlOverride))
        {
            return new ResolvedExecutionSecret(
                string.Empty,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>(),
                effectiveBaseUrl,
                activeEnv);
        }

        return null;
    }

    private async Task<ConnectionEnvironmentConfig?> GetActiveEnvironmentConfigAsync(string connectionId, CancellationToken ct)
    {
        if (_environmentConfigRepository == null)
            return null;

        return await _environmentConfigRepository.GetByConnectionAndEnvironmentAsync(connectionId, ActiveEnvironment, ct);
    }

    private async Task<ResolvedExecutionSecret?> ResolveConnectionSecretAsync(
        Connection connection,
        ConnectionEnvironmentConfig? envConfig,
        string effectiveBaseUrl,
        string activeEnv,
        CancellationToken ct)
    {
        // Environment-specific overrides take precedence over base connection credentials
        if (envConfig != null)
        {
            if (!string.IsNullOrWhiteSpace(envConfig.ExternalSecretKeyOverride))
            {
                string? envVaultValue = await _externalVaultClient.GetSecretAsync(envConfig.ExternalSecretKeyOverride, ct);
                if (!string.IsNullOrWhiteSpace(envVaultValue))
                {
                    return BuildResolvedSecret(envVaultValue, effectiveBaseUrl, activeEnv);
                }
            }

            if (!string.IsNullOrWhiteSpace(envConfig.EncryptedCredentialsJsonOverride))
            {
                string envPlaintext = _secretProtector.Unprotect(envConfig.EncryptedCredentialsJsonOverride, SecretPurposes.Connections);
                return BuildResolvedSecret(envPlaintext, effectiveBaseUrl, activeEnv);
            }
        }

        // External Vault source
        if (string.Equals(connection.SecretSource, "external_vault", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(connection.ExternalSecretKey))
        {
            string? vaultValue = await _externalVaultClient.GetSecretAsync(connection.ExternalSecretKey, ct);
            if (!string.IsNullOrWhiteSpace(vaultValue))
            {
                return BuildResolvedSecret(vaultValue, effectiveBaseUrl, activeEnv);
            }
        }

        // Internal encrypted credentials
        if (string.Equals(connection.SecretSource, "internal_encrypted", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(connection.EncryptedCredentialsJson))
        {
            string plaintext = _secretProtector.Unprotect(connection.EncryptedCredentialsJson, SecretPurposes.Connections);
            return BuildResolvedSecret(plaintext, effectiveBaseUrl, activeEnv);
        }

        // Legacy SecretRef fallback (backward compatibility for existing REST connections)
        if (!string.IsNullOrWhiteSpace(connection.SecretRef))
        {
            string? legacySecret = await _secretProvider.ResolveSecretAsync(connection.SecretRef, ct);
            if (!string.IsNullOrWhiteSpace(legacySecret))
            {
                return BuildResolvedSecret(legacySecret, effectiveBaseUrl, activeEnv);
            }
        }

        return null;
    }

    private static ResolvedExecutionSecret BuildResolvedSecret(
        string rawValue,
        string? effectiveBaseUrl,
        string? activeEnvironment)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var secretsToRedact = new List<string>();

        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            secretsToRedact.Add(rawValue);
        }

        string trimmed = rawValue.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        string val = prop.Value.ValueKind == JsonValueKind.String
                            ? prop.Value.GetString() ?? string.Empty
                            : prop.Value.GetRawText();

                        fields[prop.Name] = val;

                        if (!string.IsNullOrWhiteSpace(val) && IsSensitiveFieldName(prop.Name))
                        {
                            secretsToRedact.Add(val);
                        }
                    }
                }
            }
            catch
            {
                // Raw non-JSON secret string; keep as RawValue
            }
        }

        return new ResolvedExecutionSecret(
            rawValue,
            fields,
            secretsToRedact.Distinct(StringComparer.Ordinal).ToList(),
            effectiveBaseUrl,
            activeEnvironment);
    }

    private static bool IsSensitiveFieldName(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("password")
            || lower.Contains("pwd")
            || lower.Contains("secret")
            || lower.Contains("token")
            || lower.Contains("apikey")
            || lower.Contains("api_key")
            || lower.Contains("connectionstring")
            || lower.Contains("authorization");
    }
}
