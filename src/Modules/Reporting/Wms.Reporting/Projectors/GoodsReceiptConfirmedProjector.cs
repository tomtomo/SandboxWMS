using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Reporting.Stores;

namespace Wms.Reporting.Projectors;

// What: Idempotent projection consumer (EIP Idempotent Receiver; ADR-0005/0017) — GRConfirmed → 2 projeksi
// Why: sisi Reporting dari penerimaan barang. SATU event meng-update DUA projeksi: ReceivingSummary
// (per supplier/hari) + StockOnHandView (increment per receivedLine). Keduanya + Inbox-mark commit dalam
// SATU transaksi (Inbox-committed atomicity, ADR-0017) → cegah double-count / lost di partial failure.
// How: cek Inbox (eventId, HandlerType) → store.Apply* (find-or-create + mutate, NO SaveChanges) →
// MarkProcessed → IUnitOfWork.SaveChanges (commit projection-write + Inbox satu tx). occurredAt (dari
// envelope, BUKAN wall-clock) → bucket hari deterministik untuk rebuild.
public sealed class GoodsReceiptConfirmedProjector(
    IReceivingSummaryStore receivingSummaryStore,
    IStockOnHandViewStore stockOnHandViewStore,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "reporting.gr-confirmed";

    public async Task<Result> HandleAsync(
        Guid eventId, DateTimeOffset occurredAt, GRConfirmedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var day = DateOnly.FromDateTime(occurredAt.UtcDateTime);

        // ReceivingSummary: volume diterima + ditolak (discrepancy rate = RejectedQty/total, dihitung saat query)
        var receivedQty = message.ReceivedLines.Sum(line => line.Quantity);
        var rejectedQty = message.RejectedLines.Sum(line => line.Quantity);
        await receivingSummaryStore.ApplyConfirmedAsync(
            message.SupplierId, day, receivedQty, rejectedQty, cancellationToken);

        // StockOnHandView: per receivedLine, increment qty per (warehouse, sku, batch)
        foreach (var line in message.ReceivedLines)
            await stockOnHandViewStore.ApplyReceivedAsync(
                message.WarehouseId, line.Sku, line.Batch, line.Quantity, cancellationToken);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
