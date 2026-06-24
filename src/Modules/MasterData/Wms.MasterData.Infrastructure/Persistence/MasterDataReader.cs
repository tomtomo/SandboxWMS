using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Application.ReadModels;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — sisi-baca MasterData untuk gRPC read-API
// Why: realisasi IMasterDataReader yang di-inject gRPC service (`.Api`) — gRPC TAK menyentuh DbContext
// langsung (FF#8). AsNoTracking (read-only). Active reads tunduk global soft-delete filter; method
// *IncludingInactive me-relaks via flag IncludeInactive (TARGETED bypass, ADR-0014) lalu reset
// try/finally. Materialize-then-map (bukan projection EF) → mapping read-model in-memory bebas batasan
// translasi strongly-typed id.
internal sealed class MasterDataReader(MasterDataDbContext db) : IMasterDataReader
{
    public async Task<ProductReadModel?> GetProductAsync(string sku, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == new ProductId(sku), cancellationToken);
        return product is null ? null : ToReadModel(product);
    }

    public async Task<WarehouseReadModel?> GetWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var warehouse = await db.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == new WarehouseId(warehouseId), cancellationToken);
        return warehouse is null ? null : ToReadModel(warehouse);
    }

    public async Task<LocationReadModel?> GetLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
    {
        var location = await db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == new LocationId(locationId), cancellationToken);
        return location is null ? null : ToReadModel(location);
    }

    // What: resolve default location (warehouse + type) — AKTIF pertama urut Code (deterministik)
    // How: filter warehouse_id (strongly-typed→Guid) + type (enum→string); index (warehouse_id, type)
    // dari Phase 04a mendukung lookup ini; global soft-delete filter menjamin hanya yang aktif.
    public async Task<LocationReadModel?> GetDefaultLocationAsync(
        Guid warehouseId, LocationType type, CancellationToken cancellationToken = default)
    {
        var location = await db.Locations.AsNoTracking()
            .Where(l => l.WarehouseId == new WarehouseId(warehouseId) && l.Type == type)
            .OrderBy(l => l.Code)
            .FirstOrDefaultAsync(cancellationToken);
        return location is null ? null : ToReadModel(location);
    }

    // What: targeted soft-delete bypass (ADR-0014) — relaks HANYA isActive via flag; filter lain tetap
    public async Task<ProductReadModel?> GetProductIncludingInactiveAsync(string sku, CancellationToken cancellationToken = default)
    {
        db.IncludeInactive = true;
        try
        {
            var product = await db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == new ProductId(sku), cancellationToken);
            return product is null ? null : ToReadModel(product);
        }
        finally
        {
            db.IncludeInactive = false;
        }
    }

    // What: paginated list Product untuk list-API manajemen (CQRS read-side; ADR-0004/0011)
    // How: IncludeInactive=true (try/finally reset, ADR-0014 targeted-bypass) agar baris inactive terlihat;
    // filter isActive hanya bila HasValue; search via EF.Functions.ILike pada Name SAJA (case-insensitive,
    // ditranslate ke Postgres ILIKE) — TIDAK memfilter ProductId/SKU (strongly-typed id mungkin tak
    // ter-translate; SKU-search di-catat sebagai follow-up). Count→OrderBy(Name)→Skip/Take→materialize→map.
    public async Task<PagedResult<ProductListItem>> ListProductsAsync(
        int page, int pageSize, bool? isActive, string? search, CancellationToken ct = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        db.IncludeInactive = true;
        try
        {
            var query = db.Products.AsNoTracking();
            if (isActive.HasValue)
                query = query.Where(p => p.IsActive == isActive.Value);
            // TODO-FOLLOWUP: SKU-search — ProductId (strongly-typed) belum difilter (translasi tak terjamin).
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search}%"));

            var totalCount = await query.CountAsync(ct);

            var products = await query
                .OrderBy(p => p.Name)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .ToListAsync(ct);

            var items = products
                .Select(p => new ProductListItem(
                    p.Id.Value, p.Name, p.Uom, p.BatchTrackingRequired,
                    p.ExpiryTrackingRequired, p.QcRequiredOnReceipt, p.ShelfLifeDays, p.IsActive))
                .ToList();

            return new PagedResult<ProductListItem>(items, safePage, safeSize, totalCount);
        }
        finally
        {
            db.IncludeInactive = false;
        }
    }

    // What: paginated list Warehouse untuk list-API manajemen (CQRS read-side; ADR-0004/0011)
    // How: IncludeInactive bypass (try/finally) → filter isActive opsional → Count → OrderBy(Name) → page → map.
    public async Task<PagedResult<WarehouseListItem>> ListWarehousesAsync(
        int page, int pageSize, bool? isActive, CancellationToken ct = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        db.IncludeInactive = true;
        try
        {
            var query = db.Warehouses.AsNoTracking();
            if (isActive.HasValue)
                query = query.Where(w => w.IsActive == isActive.Value);

            var totalCount = await query.CountAsync(ct);

            var warehouses = await query
                .OrderBy(w => w.Name)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .ToListAsync(ct);

            var items = warehouses
                .Select(w => new WarehouseListItem(w.Id.Value, w.Name, w.Address, w.IsActive))
                .ToList();

            return new PagedResult<WarehouseListItem>(items, safePage, safeSize, totalCount);
        }
        finally
        {
            db.IncludeInactive = false;
        }
    }

    // What: paginated list Location untuk list-API manajemen (CQRS read-side; ADR-0004/0011)
    // How: IncludeInactive bypass (try/finally) → filter warehouseId/type/isActive opsional → Count →
    // OrderBy(Code) → page → materialize → map (Type=enum→string via ToString(), bebas batasan translasi).
    public async Task<PagedResult<LocationListItem>> ListLocationsAsync(
        int page, int pageSize, Guid? warehouseId, LocationType? type, bool? isActive, CancellationToken ct = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        db.IncludeInactive = true;
        try
        {
            var query = db.Locations.AsNoTracking();
            if (warehouseId.HasValue)
                query = query.Where(l => l.WarehouseId == new WarehouseId(warehouseId.Value));
            if (type.HasValue)
                query = query.Where(l => l.Type == type.Value);
            if (isActive.HasValue)
                query = query.Where(l => l.IsActive == isActive.Value);

            var totalCount = await query.CountAsync(ct);

            var locations = await query
                .OrderBy(l => l.Code)
                .Skip((safePage - 1) * safeSize)
                .Take(safeSize)
                .ToListAsync(ct);

            var items = locations
                .Select(l => new LocationListItem(
                    l.Id.Value, l.WarehouseId.Value, l.Type.ToString(), l.Code, l.IsActive))
                .ToList();

            return new PagedResult<LocationListItem>(items, safePage, safeSize, totalCount);
        }
        finally
        {
            db.IncludeInactive = false;
        }
    }

    private static ProductReadModel ToReadModel(Product product) => new(
        product.Id.Value, product.Name, product.Uom, product.BatchTrackingRequired,
        product.ExpiryTrackingRequired, product.QcRequiredOnReceipt, product.ShelfLifeDays);

    private static WarehouseReadModel ToReadModel(Warehouse warehouse) =>
        new(warehouse.Id.Value, warehouse.Name, warehouse.Address);

    private static LocationReadModel ToReadModel(Location location) =>
        new(location.Id.Value, location.WarehouseId.Value, location.Type, location.Code);
}
