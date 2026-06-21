using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inventory.Domain;

// What: Aggregate Root (DDD) — Stock balance fisik, walking-skeleton minimal (Phase 01c)
// Why: hasil GRConfirmed (lineStatus=Good) masuk sebagai Stock OnHand. Batch/expiry/
// location + transisi lifecycle (putaway→Available, allocate, pick) menyusul di 03b;
// di sini cukup membuktikan event chain menghasilkan balance.
// How: factory CreateOnHand memvalidasi → Result<Stock>; tak me-raise event (Inventory
// pure consumer di 01c — belum emit StockAllocated, itu Phase 03). IAuditable via base
// AuditableAggregateRoot → created_by terisi SYSTEM saat consumer (origin mesin) membuatnya.
public sealed class Stock : AuditableAggregateRoot<StockId>
{
    public string WarehouseId { get; private set; } = null!;

    public string Sku { get; private set; } = null!;

    public int Quantity { get; private set; }

    public Guid SourceGoodsReceiptId { get; private set; }

    public StockStatus Status { get; private set; }

    private Stock() { }

    private Stock(StockId id, string warehouseId, string sku, int quantity, Guid sourceGoodsReceiptId)
        : base(id)
    {
        WarehouseId = warehouseId;
        Sku = sku;
        Quantity = quantity;
        SourceGoodsReceiptId = sourceGoodsReceiptId;
        Status = StockStatus.OnHand;
    }

    public static Result<Stock> CreateOnHand(
        StockId id, string warehouseId, string sku, int quantity, Guid sourceGoodsReceiptId)
    {
        if (string.IsNullOrWhiteSpace(warehouseId))
            return Result.Failure<Stock>(StockErrors.MissingWarehouse);
        if (string.IsNullOrWhiteSpace(sku))
            return Result.Failure<Stock>(StockErrors.MissingSku);
        if (quantity <= 0)
            return Result.Failure<Stock>(StockErrors.NonPositiveQuantity);

        return Result.Success(new Stock(id, warehouseId, sku, quantity, sourceGoodsReceiptId));
    }
}
