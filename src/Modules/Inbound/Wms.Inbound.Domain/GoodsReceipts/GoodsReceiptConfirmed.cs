using Wms.BuildingBlocks.Domain.Events;

namespace Wms.Inbound.Domain;

// What: Domain Event (DDD; emission policy ADR-0026)
// Why: menandai fakta bisnis "GR dikonfirmasi" di dalam aggregate, in-process.
// Diterjemahkan jadi integration event GRConfirmedV1 di Application sebelum
// menyeberang broker (ADR-0005) — tipe domain ini tak pernah jadi wire-contract.
public sealed record GoodsReceiptConfirmed(
    GoodsReceiptId GoodsReceiptId,
    string WarehouseId,
    IReadOnlyList<GoodsReceiptConfirmedLine> Lines) : IDomainEvent;

// What: snapshot line di dalam domain event (in-process)
public sealed record GoodsReceiptConfirmedLine(string Sku, int Quantity);
