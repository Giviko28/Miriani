using Application.Ai;
using Application.Common;
using Application.Documents;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Documents;

/// <summary>
/// Handles document intake: stores the bytes via IFileStorage, records org-scoped metadata,
/// and indexes the content into the vector store via the AI service. Status reflects whether
/// indexing succeeded (Indexed) or failed (Failed) so the UI can surface it.
/// </summary>
public class DocumentService(
    AppDbContext db,
    IFileStorage storage,
    IAiService ai,
    ICurrentUser currentUser,
    ILogger<DocumentService> logger) : IDocumentService
{
    public async Task<DocumentDto> UploadAsync(UploadFile file, UserRole accessRole, CancellationToken ct = default)
    {
        var orgId = currentUser.OrgId;

        // Read once: the same bytes are persisted to storage and sent to the AI service.
        using var ms = new MemoryStream();
        await file.Content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var (storagePath, size) = await storage.SaveAsync(orgId, file.FileName, new MemoryStream(bytes), ct);

        var doc = new Document
        {
            OrgId = orgId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = size,
            StoragePath = storagePath,
            Status = DocumentStatus.Processing,
            AccessRole = accessRole,
            UploadedByUserId = currentUser.UserId,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);

        try
        {
            await ai.IngestAsync(orgId, doc.Id, doc.FileName, accessRole, bytes, ct);
            doc.Status = DocumentStatus.Indexed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index document {DocId}", doc.Id);
            doc.Status = DocumentStatus.Failed;
        }
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
