namespace Wms.Reporting.Directory;

// What: Port (ACL; Hexagonal) — resolve operator id → username via Auth read-API (ADR-0011)
// Why: projection Reporting key-by OperatorId (stabil + rebuild-deterministic; username Auth-owned & bisa
// berubah → JANGAN denormalize ke projection, ADR-0017). DB-per-service (ADR-0010) larang baca tabel Auth
// langsung → akses via read-API gRPC. Port di modul (ACL, di-stub di integration test), adapter gRPC di host.
// How: GetUsernameAsync return null bila user tak ditemukan (RpcException NotFound → null di adapter) →
// caller fallback ke id mentah. Enrichment terjadi di READ path (query endpoint), bukan projection path.
public interface IUserDirectory
{
    Task<string?> GetUsernameAsync(string userId, CancellationToken cancellationToken = default);
}
