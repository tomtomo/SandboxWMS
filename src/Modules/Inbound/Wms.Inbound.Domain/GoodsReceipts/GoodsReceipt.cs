using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inbound.Domain;

// What: Aggregate Root (DDD) — GoodsReceipt, walking-skeleton minimal (Phase 01c)
// Why: satu-satunya pintu konsistensi penerimaan barang; invariant (warehouse, lines,
// qty) ditegakkan di factory, dan HANYA aggregate yang me-raise domain event
// (emission policy ADR-0026). Discrepancy/two-axis (ADR-0013) menyusul di 03a.
// How: Create memvalidasi → Result<GoodsReceipt>; Confirm transisi InProgress→
// Confirmed lalu RaiseDomainEvent(GoodsReceiptConfirmed) — di-translate jadi
// integration event GRConfirmedV1 + ditulis Outbox di Application (ADR-0005). IAuditable
// via base AuditableAggregateRoot → created_by/modified_by terisi dari ICurrentUser (HTTP).
public sealed class GoodsReceipt : AuditableAggregateRoot<GoodsReceiptId>
{
    private readonly List<GoodsReceiptLine> _lines = new();

    public string WarehouseId { get; private set; } = null!;

    public GoodsReceiptStatus Status { get; private set; }

    public IReadOnlyCollection<GoodsReceiptLine> Lines => _lines.AsReadOnly();

    private GoodsReceipt() { }

    private GoodsReceipt(GoodsReceiptId id, string warehouseId) : base(id)
    {
        WarehouseId = warehouseId;
        Status = GoodsReceiptStatus.InProgress;
    }

    // What: factory + invariant guard (Result pattern, ADR-0019)
    // Why: konstruksi divalidasi di satu pintu; kegagalan = Error sebagai nilai, bukan throw.
    public static Result<GoodsReceipt> Create(
        GoodsReceiptId id, string warehouseId, IReadOnlyCollection<GoodsReceiptLineInput> lines)
    {
        if (string.IsNullOrWhiteSpace(warehouseId))
            return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.MissingWarehouse);

        if (lines.Count == 0)
            return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.NoLines);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Sku))
                return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.MissingSku);
            if (line.Quantity <= 0)
                return Result.Failure<GoodsReceipt>(GoodsReceiptErrors.NonPositiveQuantity);
        }

        var goodsReceipt = new GoodsReceipt(id, warehouseId);
        foreach (var line in lines)
            goodsReceipt._lines.Add(new GoodsReceiptLine(line.Sku, line.Quantity));

        return Result.Success(goodsReceipt);
    }

    // What: transisi state + domain-event emission (ADR-0026)
    // Why: event hanya di-raise pada fakta sukses; guard gagal (sudah Confirmed)
    // kembalikan Conflict tanpa emit.
    public Result Confirm()
    {
        if (Status == GoodsReceiptStatus.Confirmed)
            return Result.Failure(GoodsReceiptErrors.AlreadyConfirmed);

        Status = GoodsReceiptStatus.Confirmed;

        RaiseDomainEvent(new GoodsReceiptConfirmed(
            Id,
            WarehouseId,
            _lines.Select(line => new GoodsReceiptConfirmedLine(line.Sku, line.Quantity)).ToList()));

        return Result.Success();
    }
}
