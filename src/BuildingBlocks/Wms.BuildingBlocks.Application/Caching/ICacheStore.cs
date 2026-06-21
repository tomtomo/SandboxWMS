namespace Wms.BuildingBlocks.Application.Caching;

// What: Port — cache-aside store (Hexagonal; ADR-0011 / ADR-0002)
// Why: read-API MasterData read-heavy & jarang berubah → hasil di-cache pola CACHE-ASIDE (lazy load,
// populate-on-miss, TTL-first). Sebagai port, dikonsumsi sisi konsumen (decorator read-port) tanpa
// tahu backend konkret: InMemory (Local), StackExchange.Redis untuk Azure Cache / GCP Memorystore
// (cloud) — adapter per-cloud yang memilih (netral layanan, FF#1). Cache = optimasi sisi konsumen,
// BUKAN store kebenaran (ADR-0011).
// How: GetAsync return null saat MISS (no-throw friendly → caller populate dari authority lalu Set);
// SetAsync simpan dengan TTL (staleness terbatas waktu); RemoveAsync invalidasi eksplisit — jalur
// event-driven `ProductUpdated`→Remove DICATAT-TAK-DIAKTIFKAN (ADR-0011 amendment), TTL-first default.
public interface ICacheStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
