using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inbound.Contracts;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;

// What: Idempotent integration-event consumer (EIP Idempotent Receiver; ADR-0005)
// Why: sisi Inventory dari event chain — GRConfirmed → Stock + (untuk Good) PutawayTask. Anti-Corruption
// Layer: menerjemahkan contract asing GRConfirmedV1 (Status string) ke model Inventory sendiri (ADR-0005).
//   Good   → Stock(OnHand) di receiving area + PutawayTask (Assigned),
//   QcHold → Stock(Quarantine) di quarantine area, TANPA PutawayTask (tak masuk rak reguler).
// Phase 04a follow-up: receiving/quarantine area kini DI-RESOLVE via MasterData read-API (ILocationCatalog,
// gRPC + cache-aside) — MENGGANTIKAN seed konstanta InventoryLocations; lokasi default tak terkonfigurasi
// → loud-fail (DLQ). Delivery at-least-once → wajib idempotent.
// How: cek Inbox → resolve lokasi default per peran (sekali per warehouse, hanya yang dibutuhkan branch) →
// per receivedLine buat Stock + PutawayTask bila OnHand; MarkProcessed; IUnitOfWork commit (satu transaksi).
public sealed class GoodsReceiptConfirmedConsumer(
    ILocationCatalog locationCatalog,
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

    // What: saran destinasi putaway (placeholder putaway strategy) — overview §B: strategy (closest-empty-
    // bin/ABC/chaotic) = config INTERNAL Inventory, OUT-OF-SCOPE. Bukan lokasi MasterData → tetap konstanta;
    // operator override via actualDestination saat CompletePutaway.
    private const string SuggestedRack = "RACK-A1";

    // What: status receivedLine di luar {Good, QcHold} = contract drift → loud-fail (DLQ), bukan salah-tempat diam
    private static readonly Error UnknownLineStatus =
        Error.Validation("inventory.unknown_line_status", "receivedLine.status di luar {Good, QcHold}.");

    // What: lokasi default (receiving/quarantine) tak ditemukan di MasterData → loud-fail (DLQ), bukan diam
    private static readonly Error MissingDefaultLocation =
        Error.Validation("inventory.missing_default_location",
            "Default location (receiving/quarantine) tak ditemukan di MasterData untuk warehouse.");

    public async Task<Result> HandleAsync(
        Guid eventId, GRConfirmedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        // resolve lokasi default per peran via MasterData (cache-aside) — hanya yang dibutuhkan branch
        // (Good → ReceivingArea, QcHold → QuarantineArea), sekali per pesan (satu warehouseId).
        var receivingArea = message.ReceivedLines.Any(line => line.Status == StatusGood)
            ? await locationCatalog.GetDefaultLocationCodeAsync(message.WarehouseId, LocationKind.ReceivingArea, cancellationToken)
            : null;
        var quarantineArea = message.ReceivedLines.Any(line => line.Status == StatusQcHold)
            ? await locationCatalog.GetDefaultLocationCodeAsync(message.WarehouseId, LocationKind.QuarantineArea, cancellationToken)
            : null;

        foreach (var line in message.ReceivedLines)
        {
            var stockResult = CreateStock(message, line, receivingArea, quarantineArea);
            if (stockResult.IsFailure)
                return Result.Failure(stockResult.Error);

            var stock = stockResult.Value;
            await stockRepository.AddAsync(stock, cancellationToken);

            // Good → Stock OnHand memicu satu PutawayTask (source = lokasi receiving stock); QcHold → tak.
            if (stock.Status == StockStatus.OnHand)
            {
                await putawayTaskRepository.AddAsync(
                    PutawayTask.Assign(
                        PutawayTaskId.New(), stock.Id, stock.LocationId, SuggestedRack, assignedTo: null),
                    cancellationToken);
            }
        }

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: ACL translator — receivedLine.Status (string) → factory Stock dgn lokasi resolved (MasterData)
    // Why: lokasi null (default tak ada di MasterData) → MissingDefaultLocation (loud-fail); status di luar
    // {Good, QcHold} → UnknownLineStatus (contract drift → DLQ).
    private static Result<Stock> CreateStock(
        GRConfirmedV1 message, ReceivedLineV1 line, string? receivingArea, string? quarantineArea) => line.Status switch
    {
        StatusGood => receivingArea is null
            ? Result.Failure<Stock>(MissingDefaultLocation)
            : Stock.CreateOnHand(
                StockId.New(), message.WarehouseId, line.Sku, receivingArea,
                line.Batch, line.Expiry, line.Quantity, message.GrId),
        StatusQcHold => quarantineArea is null
            ? Result.Failure<Stock>(MissingDefaultLocation)
            : Stock.CreateQuarantine(
                StockId.New(), message.WarehouseId, line.Sku, quarantineArea,
                line.Batch, line.Expiry, line.Quantity, message.GrId),
        _ => Result.Failure<Stock>(UnknownLineStatus),
    };
}
