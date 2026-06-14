using Application.Common;
using Application.Documents;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Documents;

/// <summary>
/// Handles document intake: stores the bytes via IFileStorage and records org-scoped
/// metadata. Ingestion into ChromaDB (the Indexed transition) is added in Milestone 3.
/// </summary>
public class DocumentService(
    AppDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser) : IDocumentService
{
    public async Task<DocumentDto> UploadAsync(UploadFile file, UserRole accessRole, CancellationToken ct = default)
    {
        var orgId = currentUser.OrgId;
        var (storagePath, size) = await storage.SaveAsync(orgId, file.FileName, file.Content, ct);

        var doc = new Document
        {
            OrgId = orgId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = size,
            StoragePath = storagePath,
            Status = DocumentStatus.Uploaded,
            AccessRole = accessRole,
            UploadedByUserId = currentUser.UserId,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);

        return ToDto(doc);
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(CancellationToken ct = default)
    {
        var orgId = currentUser.OrgId;
        return await db.Documents
            .Where(d => d.OrgId == orgId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => ToDto(d))
            .ToListAsync(ct);
    }

    private static DocumentDto ToDto(Document d) =>
        new(d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Status, d.AccessRole, d.CreatedAt);
}
