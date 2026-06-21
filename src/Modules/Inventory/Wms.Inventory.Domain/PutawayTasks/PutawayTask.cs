using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inventory.Domain;

// What: Aggregate Root (DDD) — PutawayTask: instruksi memindah satu Stock unit receiving→rak
// Why: tiap Stock OnHand (lineStatus=Good) memicu satu instruksi putaway (overview §B1). Aggregate
// TERPISAH dari Stock, mereferensikan Stock by id (StockId) — bukan navigation — menjaga batas
// aggregate (DDD: referensikan aggregate lain via identitas). Quarantine TAK generate PutawayTask.
// How: factory Assign membuat state Assigned (+ source/suggested location dari putaway strategy seed +
// assignedTo operator). Complete (Assigned→Completed) menyimpan actualDestination; legalitas via Result
// (no-throw, FF#7). IAuditable via base. Penyelesaian task DAN transisi Stock OnHand→Available di-orkestrasi
// satu transaksi oleh handler CompletePutaway (dua aggregate, satu UoW).
public sealed class PutawayTask : AuditableAggregateRoot<PutawayTaskId>
{
    public StockId StockId { get; private set; } = null!;

    public string SourceLocationId { get; private set; } = null!;

    // What: destinasi rak yang disarankan putaway strategy (overview §B: closest empty bin) — seed di 03b
    public string SuggestedDestinationId { get; private set; } = null!;

    // What: operator yang ditugaskan — nullable (assignment operator belum di-scope; auth → 07a)
    public string? AssignedTo { get; private set; }

    // What: destinasi rak aktual hasil scan operator (set saat Complete)
    public string? ActualDestinationId { get; private set; }

    public PutawayTaskStatus Status { get; private set; }

    private PutawayTask() { }

    private PutawayTask(
        PutawayTaskId id, StockId stockId, string sourceLocationId, string suggestedDestinationId, string? assignedTo)
        : base(id)
    {
        StockId = stockId;
        SourceLocationId = sourceLocationId;
        SuggestedDestinationId = suggestedDestinationId;
        AssignedTo = assignedTo;
        Status = PutawayTaskStatus.Assigned;
    }

    // What: factory — task baru state Assigned (dibuat consumer GRConfirmed untuk Stock OnHand)
    public static PutawayTask Assign(
        PutawayTaskId id, StockId stockId, string sourceLocationId, string suggestedDestinationId, string? assignedTo)
        => new(id, stockId, sourceLocationId, suggestedDestinationId, assignedTo);

    // What: transisi Assigned → Completed (overview §B2) — operator scan stock + scan destination
    // Why: menandai putaway selesai; actualDestination jadi lokasi rak final Stock (di-set handler).
    public Result Complete(string actualDestinationId)
    {
        if (Status != PutawayTaskStatus.Assigned)
            return Result.Failure(PutawayTaskErrors.InvalidCompletion);
        if (string.IsNullOrWhiteSpace(actualDestinationId))
            return Result.Failure(PutawayTaskErrors.MissingDestination);

        ActualDestinationId = actualDestinationId;
        Status = PutawayTaskStatus.Completed;
        return Result.Success();
    }
}
