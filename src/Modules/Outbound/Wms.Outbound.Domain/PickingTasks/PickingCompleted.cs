using Wms.BuildingBlocks.Domain.Events;

namespace Wms.Outbound.Domain;

// What: Domain Event (DDD; emission policy ADR-0026 / ADR-0028)
// Why: menandai fakta bisnis "picking selesai" di dalam PickingTask aggregate, in-process. Diterjemahkan
// jadi integration event PickingCompletedV1 (published language) di Application sebelum menyeberang broker
// (ADR-0005/0028) → Inventory transisi Stock Allocated→Picked + set pickingTaskId/staging (overview §B/§C5).
// Single-aggregate fact (data dari satu PickingTask) → di-raise aggregate, bukan compose handler. Tipe domain
// ini tak pernah jadi wire-contract (ADR-0009). Batch nullable (produk tanpa batch-tracking).
public sealed record PickingCompleted(
    Guid WaveId,
    PickingTaskId PickingTaskId,
    Guid StockId,
    string Sku,
    string? Batch,
    int Qty,
    string StagingLocationId) : IDomainEvent;
