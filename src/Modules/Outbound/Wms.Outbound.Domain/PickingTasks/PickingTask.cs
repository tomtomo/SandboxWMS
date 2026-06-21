using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Outbound.Domain;

// What: Aggregate Root (DDD) — PickingTask: instruksi ambil satu Stock unit dari rak ke staging (overview §C)
// Why: tiap entry allocations[] (StockAllocated) memicu satu instruksi picking (overview §C4). Aggregate
// TERPISAH dari Wave & dari Stock (milik Inventory), mereferensikan keduanya by id (waveId/stockId) — bukan
// navigation — menjaga batas aggregate + DB-per-service (ADR-0010). Membawa snapshot alokasi (sourceLocation/
// sku/batch/qty) agar operator tahu apa & dari mana mengambil tanpa query lintas-context.
// How: factory Assign membuat state Assigned dari data alokasi. Complete (Assigned→Completed) menyimpan
// actualQty (=qty di scope; picking discrepancy out-of-scope) + stagingLocation lalu RAISE PickingCompleted.
// Legalitas via Result (no-throw, FF#7). IAuditable via base (created_by SYSTEM saat consumer membuatnya).
public sealed class PickingTask : AuditableAggregateRoot<PickingTaskId>
{
    public Guid WaveId { get; private set; }

    // What: Stock yang dialokasi (milik Inventory) — referensi by id lintas-context, tanpa FK
    public Guid StockId { get; private set; }

    public string SourceLocationId { get; private set; } = null!;

    public string Sku { get; private set; } = null!;

    public string? Batch { get; private set; }

    public int Qty { get; private set; }

    // What: operator yang ditugaskan — nullable (assignment operator belum di-scope; auth → 07a)
    public string? AssignedTo { get; private set; }

    // What: kuantitas aktual hasil pick (set saat Complete) — = Qty di scope (discrepancy out-of-scope)
    public int? ActualQty { get; private set; }

    // What: lokasi staging tujuan hasil scan operator (set saat Complete)
    public string? StagingLocationId { get; private set; }

    public PickingTaskStatus Status { get; private set; }

    private PickingTask() { }

    private PickingTask(
        PickingTaskId id, Guid waveId, Guid stockId, string sourceLocationId,
        string sku, string? batch, int qty, string? assignedTo)
        : base(id)
    {
        WaveId = waveId;
        StockId = stockId;
        SourceLocationId = sourceLocationId;
        Sku = sku;
        Batch = batch;
        Qty = qty;
        AssignedTo = assignedTo;
        Status = PickingTaskStatus.Assigned;
    }

    // What: factory — task baru state Assigned (dibuat consumer StockAllocated per allocation)
    // Why: data alokasi sudah tervalidasi di produser (Inventory FEFO) → factory langsung mengkonstruksi
    // tanpa Result, mirip PutawayTask.Assign; legalitas operasi berikutnya (Complete) yang ber-Result.
    public static PickingTask Assign(
        PickingTaskId id, Guid waveId, Guid stockId, string sourceLocationId,
        string sku, string? batch, int qty, string? assignedTo)
        => new(id, waveId, stockId, sourceLocationId, sku, batch, qty, assignedTo);

    // What: transisi Assigned → Completed + emission (overview §C5, ADR-0026/0028)
    // Why: operator scan stock + ambil + scan staging → stock pindah ke staging (Picked di Inventory).
    // actualQty di-set = Qty (scope: actualQty=qty; picking discrepancy out-of-scope global). Raise
    // PickingCompleted hanya pada fakta sukses — guard gagal = no event.
    public Result Complete(string stagingLocationId)
    {
        if (Status != PickingTaskStatus.Assigned)
            return Result.Failure(PickingTaskErrors.InvalidCompletion);
        if (string.IsNullOrWhiteSpace(stagingLocationId))
            return Result.Failure(PickingTaskErrors.MissingStagingLocation);

        ActualQty = Qty;
        StagingLocationId = stagingLocationId;
        Status = PickingTaskStatus.Completed;
        RaiseDomainEvent(new PickingCompleted(WaveId, Id, StockId, Sku, Batch, Qty, stagingLocationId));
        return Result.Success();
    }
}
