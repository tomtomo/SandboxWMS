namespace Wms.Inbound.Application.ReadModels;

// What: read DTO (CQRS read-side; ADR-0004) — ringkasan GoodsReceipt untuk list UI (Phase 04e),
// decoupled dari aggregate: Status di-flatten ke string, owned ExpectedLines di-ringkas jadi count.
public sealed record GoodsReceiptListItem(
    Guid GrId,
    string WarehouseId,
    string? PoRef,
    string? SupplierId,
    string Status,
    int ExpectedLineCount,
    DateTimeOffset CreatedAt);
