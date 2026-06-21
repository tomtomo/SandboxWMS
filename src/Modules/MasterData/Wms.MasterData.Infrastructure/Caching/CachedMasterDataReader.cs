using Wms.BuildingBlocks.Application.Caching;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Application.ReadModels;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Caching;

// What: Decorator (GoF) cache-aside di atas IMasterDataReader (ADR-0011)
// Why: read-API MasterData read-heavy & jarang berubah → bungkus reader EF dengan CACHE-ASIDE TTL-first:
// GET cache → HIT served; MISS → baca authority (inner reader) → POPULATE cache (TTL) → return. Menekan
// latency & beban authority. Decorator TRANSPARAN: gRPC service inject IMasterDataReader → menerima
// instance ini (membungkus MasterDataReader EF) tanpa tahu ada cache. TTL-first (ADR-0011): invalidasi
// event-driven (ProductUpdated→Remove) DICATAT-TAK-AKTIF. Path *IncludingInactive TAK di-cache (manajemen
// jarang; korektnес state inactive > latency).
// How: per entity key "masterdata:{type}:{id}"; hanya hasil non-null di-cache (miss tetap cepat,
// tak cache negative — produk baru muncul tanpa tunggu TTL).
internal sealed class CachedMasterDataReader(IMasterDataReader inner, ICacheStore cache) : IMasterDataReader
{
    // What: TTL cache master (ADR-0011) — staleness terbatas waktu; provisional (kalibrasi saat ada metrik)
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public async Task<ProductReadModel?> GetProductAsync(string sku, CancellationToken cancellationToken = default)
    {
        var key = $"masterdata:product:{sku}";
        var cached = await cache.GetAsync<ProductReadModel>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var product = await inner.GetProductAsync(sku, cancellationToken);
        if (product is not null)
            await cache.SetAsync(key, product, Ttl, cancellationToken);
        return product;
    }

    public async Task<WarehouseReadModel?> GetWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var key = $"masterdata:warehouse:{warehouseId}";
        var cached = await cache.GetAsync<WarehouseReadModel>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var warehouse = await inner.GetWarehouseAsync(warehouseId, cancellationToken);
        if (warehouse is not null)
            await cache.SetAsync(key, warehouse, Ttl, cancellationToken);
        return warehouse;
    }

    public async Task<LocationReadModel?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var key = $"masterdata:location:{locationId}";
        var cached = await cache.GetAsync<LocationReadModel>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var location = await inner.GetLocationAsync(locationId, cancellationToken);
        if (location is not null)
            await cache.SetAsync(key, location, Ttl, cancellationToken);
        return location;
    }

    public async Task<LocationReadModel?> GetDefaultLocationAsync(
        Guid warehouseId, LocationType type, CancellationToken cancellationToken = default)
    {
        var key = $"masterdata:location:default:{warehouseId}:{type}";
        var cached = await cache.GetAsync<LocationReadModel>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var location = await inner.GetDefaultLocationAsync(warehouseId, type, cancellationToken);
        if (location is not null)
            await cache.SetAsync(key, location, Ttl, cancellationToken);
        return location;
    }

    // What: pass-through TAK di-cache (ADR-0011) — jalur manajemen melihat inactive
    public Task<ProductReadModel?> GetProductIncludingInactiveAsync(string sku, CancellationToken cancellationToken = default)
        => inner.GetProductIncludingInactiveAsync(sku, cancellationToken);
}
