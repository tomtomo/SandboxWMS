using Wms.BuildingBlocks.Application.Pagination;
using Wms.MasterData.Application.ReadModels;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Abstractions;

// What: Read-Port (reader-delegation; ADR-0011) — sisi-baca MasterData yang dikonsumsi gRPC read-API
// Why: gRPC service ([*.Api]) delegasi ke port ini, BUKAN inject DbContext (dijaga FF#8) — boundary
// query terisolasi dari EF, dan cache-aside disisipkan sebagai DECORATOR atas port ini (Hexagonal +
// GoF Decorator). Mengembalikan read-DTO (bypass aggregate, inti CQRS). null = NOT FOUND (no-throw,
// dipetakan NotFound→404/gRPC NotFound di tepi). Query hanya melihat baris AKTIF (global soft-delete
// filter, ADR-0014) — kecuali method *IncludingInactive yang sengaja TARGETED-BYPASS.
// How: impl EF read-only di Infrastructure; decorator cache-aside membungkus method aktif (TTL-first).
public interface IMasterDataReader
{
    Task<ProductReadModel?> GetProductAsync(string sku, CancellationToken cancellationToken = default);

    Task<WarehouseReadModel?> GetWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default);

    Task<LocationReadModel?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken = default);

    // What: resolve DEFAULT location untuk (warehouse, type) — mis. ReceivingArea/QuarantineArea gudang X
    // Why: core (Inventory) butuh kode lokasi default per peran saat menempatkan Stock (overview §B/§D) —
    // beda dari GetLocation by-id. Mengembalikan yang AKTIF pertama (urut Code) dari (warehouseId, type);
    // null = tak ada lokasi tipe itu di warehouse (NotFound). Di-cache cache-aside (read-heavy, jarang ubah).
    Task<LocationReadModel?> GetDefaultLocationAsync(
        Guid warehouseId, LocationType type, CancellationToken cancellationToken = default);

    // What: targeted soft-delete BYPASS (filter-name; ADR-0014 amendment) — manajemen lihat yang inactive
    // Why: operasi tertentu (mis. re-aktivasi/audit master) harus melihat baris non-aktif — bypass yang
    // TER-TARGET nama-filter (BUKAN blanket IgnoreQueryFilters yang mematikan semua filter). TAK di-cache
    // (path jarang/manajemen), beda dari GetProductAsync yang di-cache untuk hot-path read-heavy.
    Task<ProductReadModel?> GetProductIncludingInactiveAsync(string sku, CancellationToken cancellationToken = default);

    // What: paginated list-API manajemen (CQRS read-side; ADR-0004/0011) — dipakai REST list endpoint (UI).
    // Why: list manajemen butuh melihat baris AKTIF maupun INACTIVE (filter isActive), jadi impl me-relaks
    // global soft-delete filter (flag IncludeInactive, targeted-bypass ADR-0014) lalu memfilter per isActive
    // bila diberikan. PagedResult<T> mencegah unbounded result set (Nygard, Release It!). TAK di-cache —
    // paged list dinamis (filter+page), korektnес > latency; beda dari hot-path by-id yang di-cache.
    Task<PagedResult<ProductListItem>> ListProductsAsync(
        int page, int pageSize, bool? isActive, string? search, CancellationToken ct = default);

    Task<PagedResult<WarehouseListItem>> ListWarehousesAsync(
        int page, int pageSize, bool? isActive, CancellationToken ct = default);

    Task<PagedResult<LocationListItem>> ListLocationsAsync(
        int page, int pageSize, Guid? warehouseId, LocationType? type, bool? isActive, CancellationToken ct = default);
}
