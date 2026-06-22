using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Contracts;
using Wms.Reporting.Stores;

namespace Wms.Reporting.Projectors;

// What: Idempotent projection consumer (EIP; ADR-0005/0017/0030) — StockRemoved → 2 projeksi
// Why: sisi Reporting dari stock keluar gudang (dispatch). SATU event → StockOnHandView (decrement per line)
// + DispatchSummary (satu wave + total volume). Inbox-committed atomicity (ADR-0017): keduanya + Inbox-mark
// satu transaksi. Event ini di-emit Inventory (pemilik Stock, ADR-0030) — bawa warehouse/sku/batch/qty.
// How: cek Inbox → store.Apply* (NO SaveChanges) → MarkProcessed → SaveChanges. occurredAt → bucket hari.
public sealed class StockRemovedProjector(
    IStockOnHandViewStore stockOnHandViewStore,
    IDispatchSummaryStore dispatchSummaryStore,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "reporting.stock-removed";

    public async Task<Result> HandleAsync(
        Guid eventId, DateTimeOffset occurredAt, StockRemovedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var day = DateOnly.FromDateTime(occurredAt.UtcDateTime);

        // StockOnHandView: decrement per line (warehouse, sku, batch)
        foreach (var line in message.Lines)
            await stockOnHandViewStore.ApplyRemovedAsync(
                line.WarehouseId, line.Sku, line.Batch, line.Qty, cancellationToken);

        // DispatchSummary: satu wave dispatched + total volume (lines bisa kosong = dispatch tetap tercatat)
        var volume = message.Lines.Sum(line => line.Qty);
        await dispatchSummaryStore.ApplyDispatchedAsync(day, volume, cancellationToken);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
