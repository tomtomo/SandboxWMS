using Wms.BuildingBlocks.Domain.Primitives;

namespace Wms.Inventory.Domain;

// What: Aggregate Root (DDD) — PutawayTask, walking-skeleton minimal (Phase 01c)
// Why: tiap Stock OnHand memicu satu instruksi putaway. Aggregate TERPISAH dari Stock,
// mereferensikan Stock by id (StockId) — bukan navigation — menjaga batas aggregate
// (DDD: referensikan aggregate lain via identitas).
// How: factory Assign membuat task state Assigned. suggestedDestination/assignedTo +
// Completed menyusul di 03b (butuh Location master + putaway strategy). IAuditable via base.
public sealed class PutawayTask : AuditableAggregateRoot<PutawayTaskId>
{
    public StockId StockId { get; private set; } = null!;

    public PutawayTaskStatus Status { get; private set; }

    private PutawayTask() { }

    private PutawayTask(PutawayTaskId id, StockId stockId) : base(id)
    {
        StockId = stockId;
        Status = PutawayTaskStatus.Assigned;
    }

    public static PutawayTask Assign(PutawayTaskId id, StockId stockId) => new(id, stockId);
}
