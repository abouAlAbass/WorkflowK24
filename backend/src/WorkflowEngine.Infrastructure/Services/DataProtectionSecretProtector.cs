using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using WorkflowEngine.Application.Common.Interfaces;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Purpose-isolated secret protector backed by ASP.NET Core Data Protection,
/// wrapping ciphertext in a versioned JSON envelope (<c>{"v":1,"cipher":"..."}</c>)
/// so future key/algorithm migrations remain backward-compatible.
/// </summary>
public sealed class DataProtectionSecretProtector : ISecretProtector
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly IDataProtectionProvider _dataProtectionProvider;

    public DataProtectionSecretProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
    }

    public string Protect(string plaintext, string purpose)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ValidatePurpose(purpose);

        var protector = _dataProtectionProvider.CreateProtector(purpose);
        string cipher = protector.Protect(plaintext);

        var envelope = new VersionedSecretEnvelope(CurrentVersion, cipher);
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public string Unprotect(string protectedPayload, string purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedPayload))
        {
            throw new CryptographicException("Protected secret payload cannot be null or empty.");
        }

        ValidatePurpose(purpose);

        VersionedSecretEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<VersionedSecretEnvelope>(protectedPayload, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new CryptographicException("Protected secret payload is not a valid versioned JSON envelope.", ex);
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Cipher))
        {
            throw new CryptographicException("Protected secret envelope is missing ciphertext.");
        }

        if (envelope.V != CurrentVersion)
        {
            throw new CryptographicException($"Unsupported secret envelope version '{envelope.V}'.");
        }

        var protector = _dataProtectionProvider.CreateProtector(purpose);
        return protector.Unprotect(envelope.Cipher);
    }

    private static void ValidatePurpose(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Secret protection purpose cannot be null or whitespace.", nameof(purpose));
        }
    }

    private sealed record VersionedSecretEnvelope(
        [property: JsonPropertyName("v")] int V,
        [property: JsonPropertyName("cipher")] string Cipher);
}
