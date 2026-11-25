namespace WorkflowEngine.Application.Common.Interfaces;

public record StoredBlobResult(string StoragePath, string Sha256Hash, long SizeBytes);

public interface IBlobStorageService
{
    Task<StoredBlobResult> SaveAsync(string fileName, string contentType, byte[] content, CancellationToken ct = default);
    Task<byte[]?> ReadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
