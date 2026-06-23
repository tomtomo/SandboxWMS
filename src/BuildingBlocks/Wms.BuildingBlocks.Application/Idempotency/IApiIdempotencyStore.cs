namespace Wms.BuildingBlocks.Application.Idempotency;

// What: Port (Hexagonal) — API idempotency store (ADR-0032)
// Why: middleware cek (endpoint, key) sebelum eksekusi → HIT replay response, MISS eksekusi+simpan. Port
// memungkinkan adapter per-cloud (Local: Postgres tabel; cloud: Redis/Memorystore TTL-native) tanpa
// menyentuh middleware/core. Retry-safety = idempotensi semantik per RFC 9110.
// How: TryGetAsync (replay lookup, null bila MISS) + StoreAsync (simpan response sukses pertama).
public interface IApiIdempotencyStore
{
    Task<ApiIdempotencyRecord?> TryGetAsync(
        string endpoint, string idempotencyKey, CancellationToken cancellationToken = default);

    Task StoreAsync(ApiIdempotencyRecord record, CancellationToken cancellationToken = default);
}
