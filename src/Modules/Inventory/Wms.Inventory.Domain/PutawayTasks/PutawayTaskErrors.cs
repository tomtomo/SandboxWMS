using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inventory.Domain;

// What: katalog Error domain PutawayTask (Result pattern, ADR-0019)
// Why: penyelesaian task ilegal (bukan Assigned) = Conflict (409); destination kosong = Validation (400).
public static class PutawayTaskErrors
{
    public static readonly Error NotFound =
        Error.NotFound("putaway_task.not_found", "PutawayTask tidak ditemukan.");

    public static readonly Error InvalidCompletion =
        Error.Conflict("putaway_task.invalid_completion", "hanya task Assigned yang dapat diselesaikan.");

    public static readonly Error MissingDestination =
        Error.Validation("putaway_task.missing_destination", "actualDestinationId wajib diisi.");
}
