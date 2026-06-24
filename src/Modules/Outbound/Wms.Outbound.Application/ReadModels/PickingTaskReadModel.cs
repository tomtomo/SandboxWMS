namespace Wms.Outbound.Application.ReadModels;

// What: read DTO (CQRS read-side; ADR-0004) — PickingTask untuk list UI (papan kerja operator),
// decoupled dari aggregate: Status di-flatten ke string. Membawa snapshot alokasi (sku/batch/qty)
// + assignment (AssignedTo) + tujuan (StagingLocationId) agar UI tak perlu query lintas-context.
public sealed record PickingTaskReadModel(
    Guid PickingTaskId,
    Guid WaveId,
    string Sku,
    string? Batch,
    int Qty,
    string? AssignedTo,
    string Status,
    string? StagingLocationId);
