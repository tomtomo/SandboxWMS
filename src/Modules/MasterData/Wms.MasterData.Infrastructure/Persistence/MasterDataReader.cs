using Microsoft.EntityFrameworkCore;
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

    private static ProductReadModel ToReadModel(Product product) => new(
        product.Id.Value, product.Name, product.Uom, product.BatchTrackingRequired,
        product.ExpiryTrackingRequired, product.QcRequiredOnReceipt, product.ShelfLifeDays);

    private static WarehouseReadModel ToReadModel(Warehouse warehouse) =>
        new(warehouse.Id.Value, warehouse.Name, warehouse.Address);

    private static LocationReadModel ToReadModel(Location location) =>
        new(location.Id.Value, location.WarehouseId.Value, location.Type, location.Code);
}
