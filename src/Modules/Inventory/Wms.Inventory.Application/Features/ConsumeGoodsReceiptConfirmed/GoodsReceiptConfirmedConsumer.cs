using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;

// What: Idempotent integration-event consumer (EIP Idempotent Receiver; ADR-0005)
// Why: sisi Inventory dari event chain — GRConfirmed → Stock + (untuk Good) PutawayTask. Anti-Corruption
// Layer: menerjemahkan contract asing GRConfirmedV1 (Status string) ke model Inventory sendiri (ADR-0005),
// tak meminjam tipe domain Inbound. Phase 03b: BRANCH per receivedLine.Status (overview §B1) —
//   Good   → Stock(OnHand) di receiving area + PutawayTask (Assigned),
//   QcHold → Stock(Quarantine) di quarantine area, TANPA PutawayTask (tak masuk rak reguler).
// batch/expiry kini dikonsumsi (Stock per-batch + FEFO di 03b). Delivery at-least-once → wajib idempotent.
// How: cek Inbox (eventId, HandlerType) → kalau sudah, skip. Per receivedLine buat Stock (factory sesuai
// status) + PutawayTask bila OnHand; MarkProcessed; IUnitOfWork commit — business write + inbox mark
// dalam SATU transaksi (tak ada celah efek-tanpa-mark).
public sealed class GoodsReceiptConfirmedConsumer(
    IStockRepository stockRepository,
    IPutawayTaskRepository putawayTaskRepository,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "inventory.goods-receipt-confirmed";

    // What: status line yang dikenali ACL (cermin asyncapi ReceivedLineV1.status enum)
    private const string StatusGood = "Good";
    private const string StatusQcHold = "QcHold";

    // What: status receivedLine di luar {Good, QcHold} = contract drift → loud-fail (DLQ), bukan salah-tempat diam
    private static readonly Error UnknownLineStatus =
        Error.Validation("inventory.unknown_line_status", "receivedLine.status di luar {Good, QcHold}.");

    public async Task<Result> HandleAsync(
        Guid eventId, GRConfirmedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        foreach (var line in message.ReceivedLines)
        {
            var stockResult = CreateStock(message, line);
            if (stockResult.IsFailure)
                return Result.Failure(stockResult.Error);

            var stock = stockResult.Value;
            await stockRepository.AddAsync(stock, cancellationToken);

            // Good → Stock OnHand memicu satu PutawayTask; QcHold → Quarantine, tak generate task.
            if (stock.Status == StockStatus.OnHand)
            {
                await putawayTaskRepository.AddAsync(
                    PutawayTask.Assign(
                        PutawayTaskId.New(), stock.Id,
                        InventoryLocations.ReceivingArea, InventoryLocations.SuggestedRack, assignedTo: null),
                    cancellationToken);
            }
        }

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: ACL translator — receivedLine.Status (string) → factory Stock yang tepat
    private static Result<Stock> CreateStock(GRConfirmedV1 message, ReceivedLineV1 line) => line.Status switch
    {
        StatusGood => Stock.CreateOnHand(
            StockId.New(), message.WarehouseId, line.Sku, InventoryLocations.ReceivingArea,
            line.Batch, line.Expiry, line.Quantity, message.GrId),
        StatusQcHold => Stock.CreateQuarantine(
            StockId.New(), message.WarehouseId, line.Sku, InventoryLocations.QuarantineArea,
            line.Batch, line.Expiry, line.Quantity, message.GrId),
        _ => Result.Failure<Stock>(UnknownLineStatus),
    };
}
