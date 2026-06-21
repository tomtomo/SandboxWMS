namespace Wms.BuildingBlocks.Application.Storage;

// What: Port — object storage untuk blob besar (Hexagonal; ADR-0015 / ADR-0002)
// Why: byte konten (attachment ASN/PO/foto) TIDAK disimpan di DB — ditulis ke object storage,
// row hanya simpan metadata + blobPath. Sebagai port, ia dikonsumsi Application (slice
// UploadAttachment) tanpa tahu backend konkret: filesystem lokal, Azure Blob, atau GCS — adapter
// per-cloud yang memilih (netral layanan, FF#1). blobPath = kunci logis berpola
// {grId}/{attachmentId}/{fileName} (di-generate aggregate GRAttachment).
// How: Put menulis stream konten; Get mengembalikan stream (null bila tak ada — no-throw friendly);
// Delete menghapus blob. Urutan tulis byte→row didisiplinkan di slice untuk cegah orphan.
public interface IObjectStore
{
    Task PutAsync(string blobPath, Stream content, CancellationToken cancellationToken = default);

    Task<Stream?> GetAsync(string blobPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
