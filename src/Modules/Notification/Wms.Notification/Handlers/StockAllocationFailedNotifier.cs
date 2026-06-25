using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Contracts;
using Wms.Notification.Subscriptions;

namespace Wms.Notification.Handlers;

// What: Idempotent event consumer (EIP Idempotent Receiver; ADR-0005/0017/0034) — StockAllocationFailed → alert
// Why: sisi Notification dari sinyal-gagal alokasi (ganti silent-drop, overview §B#2). Saat wave allocation tak
// memenuhi line (stock kurang/nol), pihak terkait (SPV/purchasing) diberi tahu agar bisa backorder/replenish —
// momen bisnis nyata yang dulu hilang. Recipient via SUBSCRIPTION (event tak bawa userId/warehouseId) →
// EnqueueForSubscribers lintas-warehouse (warehouseId null) atas LogicalName event; subscriber yang berlangganan
// `inventory.stock_allocation_failed.v1` menerima. Enqueue + Inbox-mark satu transaksi (Inbox-committed, ADR-0017).
// How: cek Inbox (eventId, HandlerType) → enqueuer.EnqueueForSubscribersAsync (NO SaveChanges) → MarkProcessed →
// IUnitOfWork.SaveChanges. Satu notifikasi ringkas per wave (agregasi line short) — hindari spam per-line.
public sealed class StockAllocationFailedNotifier(
    NotificationEnqueuer enqueuer,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "notification.stock-allocation-failed";

    public async Task<Result> HandleAsync(
        Guid eventId, DateTimeOffset occurredAt, StockAllocationFailedV1 message,
        CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var skuSummary = string.Join(", ", message.Lines.Select(line => $"{line.Sku} (kurang {line.ShortQty})"));
        await enqueuer.EnqueueForSubscribersAsync(
            StockAllocationFailedV1.LogicalName, warehouseId: null,
            title: "Stock kurang untuk wave",
            body: $"Wave {message.WaveId}: {message.Lines.Count} line tak teralokasi penuh — {skuSummary}.",
            eventId.ToString(), occurredAt, cancellationToken);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
