using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using WorkflowEngine.Application.Common.Interfaces;
using WorkflowEngine.Application.Common.Security;

namespace WorkflowEngine.Infrastructure.Services;

/// <summary>
/// Secure local filesystem blob storage adapter enforcing strict magic-byte validation,
/// full-payload EICAR malware screening (any file size up to 10 MB), and SHA-256 hashing.
/// </summary>
public class LocalBlobStorageService : IBlobStorageService
{
    public const long MaxAttachmentBytes = AttachmentSecurityValidator.MaxAttachmentBytes;

    private readonly string _rootDirectory;

    public LocalBlobStorageService(IConfiguration configuration)
    {
        _rootDirectory = configuration["BlobStorage:RootPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "Attachments");
    }

    public async Task<StoredBlobResult> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken ct = default)
    {
        var validated = AttachmentSecurityValidator.ValidateAndInspect(fileName, contentType, content);

        Directory.CreateDirectory(_rootDirectory);

        byte[] shaBytes = SHA256.HashData(content);
        string shaHex = Convert.ToHexString(shaBytes).ToLowerInvariant();

        string storedName = $"{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid():N}{validated.Extension}";
        string fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, storedName));

        await File.WriteAllBytesAsync(fullPath, content, ct);

        return new StoredBlobResult(storedName, shaHex, content.Length);
    }

    public async Task<byte[]?> ReadAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return null;

        string safeName = Path.GetFileName(storagePath);
        string fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, safeName));

        if (!fullPath.StartsWith(Path.GetFullPath(_rootDirectory), StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(storagePath))
        {
            string safeName = Path.GetFileName(storagePath);
            string fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, safeName));

            if (fullPath.StartsWith(Path.GetFullPath(_rootDirectory), StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        return Task.CompletedTask;
    }
}
