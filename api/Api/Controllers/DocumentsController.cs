using Application.Documents;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/documents")]
public class DocumentsController(IDocumentService documents) : ControllerBase
{
    /// <summary>Upload a document into the caller's organization knowledge base.</summary>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB cap for the MVP
    public async Task<IActionResult> Upload(
        IFormFile file, [FromForm] UserRole accessRole = UserRole.Employee, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        await using var stream = file.OpenReadStream();
        var upload = new UploadFile(file.FileName, file.ContentType, stream, file.Length);
        var dto = await documents.UploadAsync(upload, accessRole, ct);
        return CreatedAtAction(nameof(List), new { id = dto.Id }, dto);
    }

    /// <summary>List documents in the caller's organization.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await documents.ListAsync(ct));

    /// <summary>Delete a document and remove it from the vector store.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, ct);
        return NoContent();
    }
}
