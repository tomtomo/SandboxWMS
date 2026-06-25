using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Contracts;
using Wms.Outbound.Application.Abstractions;

namespace Wms.Outbound.Application.Features.ConsumeStockAllocationShortfall;

// What: Idempotent consumer StockAllocationShortfall → tandai OrderLine Short/Backordered (EIP; ADR-0005/0034)
// Why: sisi Outbound dari sinyal kekurangan alokasi (overview §C3). Inventory mengumumkan line yang stock-nya
// kurang/nol; Outbound menandai line yang bersangkutan agar TAK hilang senyap (ganti silent-drop). Mutasi
// lokal (OutboundOrder = milik Outbound, DB-per-service) → nol cross-context write. Short MENANG atas Allocated
// (presedensi di OrderLine) → urutan kedatangan event StockAllocated vs StockAllocationShortfall tak menentukan.
// CATATAN: untuk wave nol-seluruhnya, StockAllocated kosong memicu auto-cancel + release order ke New (ADR-0035);
// penandaan Short di sini jadi hint attempt terakhir, di-reset bersih saat order di-wave ulang (PlaceInWave).
// How: cek Inbox (eventId, HandlerType) → muat order terkait by orderId (best-effort, skip yang tak ada) →
// MarkLineShort(sku) → MarkProcessed → SaveChanges (satu transaksi). Delivery at-least-once → wajib idempotent.
public sealed class StockAllocationShortfallConsumer(
    IOutboundOrderRepository orderRepository,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "outbound.stock-allocation-shortfall";

    public async Task<Result> HandleAsync(
        Guid eventId, StockAllocationShortfallV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var orderIds = message.Lines.Select(line => line.OrderId).Distinct().ToArray();
        var orders = (await orderRepository.ListByIdsAsync(orderIds, cancellationToken))
            .ToDictionary(order => order.Id.Value);

        foreach (var line in message.Lines)
            if (orders.TryGetValue(line.OrderId, out var order))
                order.MarkLineShort(line.Sku);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
