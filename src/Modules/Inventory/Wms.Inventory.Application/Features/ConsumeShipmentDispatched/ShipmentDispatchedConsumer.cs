using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Outbound.Contracts;

namespace Wms.Inventory.Application.Features.ConsumeShipmentDispatched;

// What: Idempotent consumer ShipmentDispatched → remove Stock Picked (EIP; ADR-0005)
// Why: sisi Inventory dari §C6 — saat Wave Ready→Dispatched (truk keluar), semua Stock state Picked
// terikat ke wave keluar gudang (di-hapus). ACL: ShipmentDispatchedV1 (waveId) → derivasi himpunan Stock.
// How: cek Inbox (eventId, HandlerType). List Stock Picked terikat waveId → Remove tiap-nya. MarkProcessed
// + SaveChanges satu transaksi. IDEMPOTEN ganda: Inbox dedup event duplikat; DAN bila redeliver lolos,
// list kosong (sudah ter-remove) → no-op. Wave tanpa Stock Picked = no-op aman.
public sealed class ShipmentDispatchedConsumer(
    IStockRepository stockRepository,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "inventory.shipment-dispatched";

    public async Task<Result> HandleAsync(
        Guid eventId, ShipmentDispatchedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var picked = await stockRepository.ListPickedByWaveAsync(message.WaveId, cancellationToken);
        foreach (var stock in picked)
            stockRepository.Remove(stock);

        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
