using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Outbound.Domain;

// What: katalog Error domain PickingTask (Result pattern, ADR-0019)
// Why: penyelesaian task ilegal (bukan Assigned) = Conflict (409); staging location kosong = Validation (400).
public static class PickingTaskErrors
{
    public static readonly Error NotFound =
        Error.NotFound("picking_task.not_found", "PickingTask tidak ditemukan.");

    public static readonly Error InvalidCompletion =
        Error.Conflict("picking_task.invalid_completion", "hanya task Assigned yang dapat diselesaikan.");

    public static readonly Error MissingStagingLocation =
        Error.Validation("picking_task.missing_staging_location", "stagingLocationId wajib diisi.");
}
