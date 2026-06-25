using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Contracts;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Features.ConsumeStockAllocated;

// What: Idempotent consumer StockAllocated → PickingTask per allocation, atau auto-cancel wave nol-terpenuhi
// (EIP; ADR-0005/0034/0035) — overview §C4. allocations kosong = wave unfulfillable → Cancel + order→backlog.
// Why: sisi Outbound dari alokasi — setelah Inventory mengalokasi stock FEFO, Outbound membuat satu
// PickingTask per entry allocations[] (Assigned) lalu mencatat id-nya di Wave (untuk gate Ready). ACL:
// StockAllocatedV1 asing (Inventory) → model Outbound sendiri (PickingTask); StockId/LocationId/Batch/Qty
// di-snapshot ke task agar operator tahu apa & dari mana mengambil tanpa query lintas-context (DB-per-service).
// How: cek Inbox (eventId, HandlerType). Load Wave (harus ada — kita yang buat). Per allocation: Assign
// PickingTask → AddAsync, kumpulkan id. wave.AttachPickingTasks(ids) (guard Active di domain). MarkProcessed
// + SaveChanges = SATU transaksi (anti dual-write). Delivery at-least-once → wajib idempotent.
public sealed class StockAllocatedConsumer(
    IWaveRepository waveRepository,
    IPickingTaskRepository pickingTaskRepository,
    IOutboundOrderRepository orderRepository,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "outbound.stock-allocated";

    public async Task<Result> HandleAsync(
        Guid eventId, StockAllocatedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var wave = await waveRepository.GetByIdAsync(new WaveId(message.WaveId), cancellationToken);
        if (wave is null)
            return Result.Failure(WaveErrors.NotFound);

        var taskIds = new List<Guid>();
        foreach (var allocation in message.Allocations)
        {
            var task = PickingTask.Assign(
                PickingTaskId.New(), message.WaveId, allocation.StockId, allocation.LocationId,
                allocation.Sku, allocation.Batch, allocation.Qty, assignedTo: null);
            await pickingTaskRepository.AddAsync(task, cancellationToken);
            taskIds.Add(task.Id.Value);
        }

        var attach = wave.AttachPickingTasks(taskIds);
        if (attach.IsFailure)
            return Result.Failure(attach.Error);

        if (message.Allocations.Count == 0)
        {
            // ADR-0035: wave nol-terpenuhi (stock nol) → auto-cancel + lepas order ke backlog (New). Cegah
            // hang (0 task → MarkReady mustahil). Order TAK dibatalkan mati — re-waveable saat stock tiba.
            // StockAllocated = penanda alokasi-selesai per-wave (sekali, Inbox-dedup) → keputusan deterministik.
            var cancel = wave.Cancel();
            if (cancel.IsFailure)
                return Result.Failure(cancel.Error);
            await ReleaseOrdersAsync(wave, cancellationToken);
        }
        else
        {
            // ADR-0034: tandai OrderLine teralokasi (Pending→Allocated; Short menang bila line juga short).
            // Best-effort: muat order yang ADA dari orderId alokasi; PickingTask tetap output utama (tak gagal
            // bila order tak ditemukan). Mutasi tracked → ter-flush di SaveChanges (satu transaksi, DB-per-service).
            await MarkLinesAllocatedAsync(message.Allocations, cancellationToken);
        }

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ADR-0035: lepas semua order wave yang di-cancel kembali ke backlog (InProgress→New, clear WaveId)
    private async Task ReleaseOrdersAsync(Wave wave, CancellationToken cancellationToken)
    {
        var orders = await orderRepository.ListByIdsAsync(wave.OrderIds, cancellationToken);
        foreach (var order in orders)
            order.ReleaseFromWave();
    }

    private async Task MarkLinesAllocatedAsync(
        IReadOnlyList<StockAllocationV1> allocations, CancellationToken cancellationToken)
    {
        var orderIds = allocations.Select(allocation => allocation.OrderId).Distinct().ToArray();
        if (orderIds.Length == 0)
            return;

        var orders = (await orderRepository.ListByIdsAsync(orderIds, cancellationToken))
            .ToDictionary(order => order.Id.Value);

        foreach (var allocation in allocations)
            if (orders.TryGetValue(allocation.OrderId, out var order))
                order.MarkLineAllocated(allocation.Sku);
    }
}
