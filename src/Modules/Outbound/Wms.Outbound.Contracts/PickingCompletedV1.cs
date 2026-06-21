namespace Wms.Outbound.Contracts;

// What: Integration Event ke-5 (Published Language; ADR-0028) — Outbound → Inventory
// Why: overview §B mewajibkan transisi Stock Allocated→Picked dipicu PickingTask Assigned→Completed
// di Outbound, tapi katalog 4-event asli tak punya kanalnya — gap yang direalisasikan ADR-0028.
// Karena Stock milik Inventory & PickingTask milik Outbound, DB-per-service (ADR-0010) melarang
// Outbound menulis store Inventory → perubahan state harus mengalir via event (ADR-0005).
// How: record immutable; payload inti = waveId/pickingTaskId/stockId + staging location (data hasil
// picking yang mengisi Stock.Picked). LogicalName terdaftar di asyncapi.yaml (FF#11). Batch nullable
// (produk tanpa batch-tracking). Lahir 03b consumer-first; emitter PickingTask menyusul 03c.
public sealed record PickingCompletedV1(
    Guid WaveId,
    Guid PickingTaskId,
    Guid StockId,
    string Sku,
    string? Batch,
    int Qty,
    string StagingLocationId)
{
    public const string LogicalName = "outbound.picking_completed.v1";
}
