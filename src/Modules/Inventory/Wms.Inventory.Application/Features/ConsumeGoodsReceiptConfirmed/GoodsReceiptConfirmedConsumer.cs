using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;

// What: Idempotent integration-event consumer (EIP Idempotent Receiver; ADR-0005)
// Why: sisi Inventory dari event chain — GRConfirmed → Stock(OnHand) + PutawayTask
// (Assigned). Anti-Corruption Layer: menerjemahkan contract asing GRConfirmedV1 ke model
// Inventory sendiri (ADR-0005), tak meminjam tipe domain Inbound. Delivery at-least-once
// → wajib idempotent.
// How: cek Inbox (eventId, HandlerType) → kalau sudah, skip. Per receivedLine buat Stock
// + PutawayTask via repository port; MarkProcessed; IUnitOfWork commit — business write +
// inbox mark dalam SATU transaksi (tak ada celah efek-tanpa-mark).
public sealed class GoodsReceiptConfirmedConsumer(
    IStockRepository stockRepository,
    IPutawayTaskRepository putawayTaskRepository,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "inventory.goods-receipt-confirmed";

    public async Task<Result> HandleAsync(
        Guid eventId, GRConfirmedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        foreach (var line in message.ReceivedLines)
        {
            var stockResult = Stock.CreateOnHand(
                StockId.New(), message.WarehouseId, line.Sku, line.Quantity, message.GrId);
            if (stockResult.IsFailure)
                return Result.Failure(stockResult.Error);

            await stockRepository.AddAsync(stockResult.Value, cancellationToken);
            await putawayTaskRepository.AddAsync(
                PutawayTask.Assign(PutawayTaskId.New(), stockResult.Value.Id), cancellationToken);
        }

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
