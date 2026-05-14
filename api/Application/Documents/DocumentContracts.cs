using Domain.Enums;

namespace Application.Documents;

public record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentStatus Status,
    UserRole AccessRole,
    DateTime CreatedAt);

/// <summary>A file being uploaded — abstracts away ASP.NET's IFormFile.</summary>
public record UploadFile(string FileName, string ContentType, Stream Content, long Length);

/// <summary>Stores uploaded document bytes and records their metadata, scoped to the org.</summary>
public interface IDocumentService
{
    Task<DocumentDto> UploadAsync(UploadFile file, UserRole accessRole, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDto>> ListAsync(CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Persists raw file bytes to a storage backend and returns a retrievable path.</summary>
public interface IFileStorage
{
    Task<(string StoragePath, long SizeBytes)> SaveAsync(Guid orgId, string fileName, Stream content, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
