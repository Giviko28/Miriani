using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Metadata for a file uploaded into a company's knowledge base. The bytes live on disk
/// (data/uploads); only the pointer and lifecycle state are stored in SQL. The minimum
/// AccessRole gates who can retrieve this document's content via RAG.
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrgId { get; set; }
    public Organization? Org { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;

    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    /// <summary>Minimum role required to retrieve this document in RAG answers.</summary>
    public UserRole AccessRole { get; set; } = UserRole.Employee;

    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
