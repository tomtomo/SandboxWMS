using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Outbound.Domain;

// What: Aggregate Root (DDD) — Wave: grouping OutboundOrder untuk picking/dispatch bersama (overview §C)
// Why: satu-satunya entry point konsistensi siklus wave. Active saat dibuat; Ready saat SEMUA PickingTask
// Completed (gate agregasi di domain, ADR-0026); Dispatched saat SPV dispatch (raise ShipmentDispatched).
// Wave mereferensikan order & picking task LAIN via identitas (Guid), bukan navigation (DDD: aggregate
// referensi yang lain by id) — orderIds di-set saat Activate, pickingTaskIds saat StockAllocated dikonsumsi.
// allocations[] overview §C TIDAK disimpan terpisah: terrealisasi sebagai PickingTask records (tiap task
// membawa stockId/sourceLocation/sku/batch/qty) yang ditunjuk pickingTaskIds — hindari state redundan/drift.
// How: factory Activate → Result<Wave>; transisi via Result (no-throw, FF#7); MarkReady menegakkan gate
// "semua task completed" (completion facts disuplai handler dari query PickingTask aggregates).
public sealed class Wave : AuditableAggregateRoot<WaveId>
{
    private readonly List<Guid> _orderIds = new();
    private readonly List<Guid> _pickingTaskIds = new();

    public WaveStatus Status { get; private set; }

    public IReadOnlyCollection<Guid> OrderIds => _orderIds.AsReadOnly();

    public IReadOnlyCollection<Guid> PickingTaskIds => _pickingTaskIds.AsReadOnly();

    private Wave() { }

    private Wave(WaveId id, IEnumerable<Guid> orderIds)
        : base(id)
    {
        _orderIds.AddRange(orderIds);
        Status = WaveStatus.Active;
    }

    // What: factory — wave baru state Active (overview §C2, SPV buat wave dari beberapa order)
    // Why: minimal satu order; emit WaveReleased dikomposisi handler CreateWave (cross-aggregate lines).
    public static Result<Wave> Activate(WaveId id, IReadOnlyCollection<Guid> orderIds)
    {
        if (orderIds.Count == 0)
            return Result.Failure<Wave>(WaveErrors.NoOrders);

        return Result.Success(new Wave(id, orderIds));
    }

    // What: isi pickingTaskIds dari hasil alokasi (overview §C4, dipicu StockAllocated)
    // Why: tiap entry allocations[] → satu PickingTask; wave mencatat id-nya untuk gate Ready. Hanya legal
    // saat Active (alokasi terjadi atas wave yang masih aktif).
    public Result AttachPickingTasks(IEnumerable<Guid> pickingTaskIds)
    {
        if (Status != WaveStatus.Active)
            return Result.Failure(WaveErrors.NotActive);

        _pickingTaskIds.AddRange(pickingTaskIds);
        return Result.Success();
    }

    // What: transisi Active → Ready — gate agregasi "semua PickingTask Completed" (overview §C5, ADR-0026)
    // Why: wave siap dispatch HANYA saat tiap picking task-nya selesai. Gate ditegakkan DI SINI (domain):
    // wave memverifikasi setiap pickingTaskId-nya ada di himpunan completed yang disuplai handler (fakta
    // completion datang dari PickingTask aggregates yang di-query handler — DB-per-aggregate dalam satu context).
    public Result MarkReady(IReadOnlyCollection<Guid> completedPickingTaskIds)
    {
        if (Status != WaveStatus.Active)
            return Result.Failure(WaveErrors.NotAllPicked);
        if (_pickingTaskIds.Count == 0 || !_pickingTaskIds.All(completedPickingTaskIds.Contains))
            return Result.Failure(WaveErrors.NotAllPicked);

        Status = WaveStatus.Ready;
        return Result.Success();
    }

    // What: transisi Ready → Dispatched + emission (overview §C6, ADR-0026) — terminal
    // Why: SPV eksekusi dispatch (truk keluar); raise ShipmentDispatched agar Inventory remove Stock Picked.
    // Event hanya di-raise pada fakta sukses — guard gagal = Conflict tanpa emit.
    public Result Dispatch()
    {
        if (Status != WaveStatus.Ready)
            return Result.Failure(WaveErrors.InvalidDispatch);

        Status = WaveStatus.Dispatched;
        RaiseDomainEvent(new ShipmentDispatched(Id));
        return Result.Success();
    }
}
