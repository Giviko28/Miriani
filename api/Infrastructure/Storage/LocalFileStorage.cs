using Application.Documents;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Storage;

/// <summary>
/// Saves uploaded bytes to the local filesystem under an org-scoped folder. Good enough
/// for the MVP/demo; a cloud blob backend can replace this behind IFileStorage later.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration config)
    {
        _root = config["Storage:UploadsRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "uploads");
    }

    public async Task<(string StoragePath, long SizeBytes)> SaveAsync(
        Guid orgId, string fileName, Stream content, CancellationToken ct = default)
    {
        var orgFolder = Path.Combine(_root, orgId.ToString());
        Directory.CreateDirectory(orgFolder);

        var safeName = Path.GetFileName(fileName);
        var unique = $"{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(orgFolder, unique);

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            await content.CopyToAsync(fs, ct);
        }

        var size = new FileInfo(fullPath).Length;
        return (fullPath, size);
    }
}
