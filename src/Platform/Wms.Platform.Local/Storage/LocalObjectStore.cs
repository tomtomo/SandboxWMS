using Wms.BuildingBlocks.Application.Storage;

namespace Wms.Platform.Local.Storage;

// What: Adapter Local untuk port IObjectStore (filesystem) — mirror LocalDeadLetterStore/LocalAuditLogStore
// Why: implementasi konkret object storage untuk environment lokal — blob ditulis sebagai file di
// bawah satu root directory. Adapter cloud (Azure Blob/GCS) punya implementasi sendiri tanpa
// menyentuh core (Hexagonal). blobPath ('/'-separated) dipetakan ke path filesystem.
// How: segmen blobPath di-sanitasi (buang ".."/"."/empty → cegah path-traversal) lalu Path.Combine
// dgn root; Put bikin direktori + tulis file; Get buka read (null bila absen); Delete hapus file.
public sealed class LocalObjectStore(string rootPath) : IObjectStore
{
    public async Task PutAsync(string blobPath, Stream content, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(blobPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> GetAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(blobPath);
        Stream? stream = File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(blobPath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    // What: petakan blobPath logis → path filesystem aman di bawah root (cegah path-traversal)
    private string Resolve(string blobPath)
    {
        var segments = blobPath
            .Split('/', '\\')
            .Where(segment => segment is not ("" or "." or ".."));
        return Path.Combine([rootPath, .. segments]);
    }
}
