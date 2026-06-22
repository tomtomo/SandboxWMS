using System.Text.Json;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Contracts;
using Wms.Outbound.Contracts;

namespace Wms.Inventory.Application.Features.ConsumeShipmentDispatched;

// What: Idempotent consumer ShipmentDispatched → remove Stock Picked + emit StockRemoved (EIP; ADR-0005/0030)
// Why: sisi Inventory dari §C6 — saat Wave Ready→Dispatched (truk keluar), semua Stock state Picked
// terikat ke wave keluar gudang (di-hapus). ACL: ShipmentDispatchedV1 (waveId) → derivasi himpunan Stock.
// Karena Inventory PEMILIK Stock (punya warehouse/sku/batch/qty), ia mengemit StockRemovedV1 ke Reporting
// (ADR-0030): StockOnHandView decrement + DispatchSummary — Outbound `shipment_dispatched` tak punya data
// ini. How: cek Inbox (eventId, HandlerType). List Stock Picked terikat waveId → snapshot lines → Remove
// tiap-nya → Enqueue StockRemoved → MarkProcessed + SaveChanges SATU transaksi (anti dual-write). IDEMPOTEN
// ganda: Inbox dedup event duplikat (sebelum emit, jadi tak double-emit); redeliver yg lolos → list kosong
// → no-op. Wave tanpa Stock Picked = StockRemoved lines kosong (dispatch tetap tercatat di DispatchSummary).
public sealed class ShipmentDispatchedConsumer(
    IStockRepository stockRepository,
    IIntegrationEventOutbox outbox,
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

        // snapshot dimensi SEBELUM remove (entity bisa detached pasca-Remove) — warehouse/sku/batch/qty
        // adalah data milik Inventory yang Reporting butuh untuk decrement + dispatch volume (ADR-0030).
        var lines = picked
            .Select(stock => new StockRemovedLineV1(stock.WarehouseId, stock.Sku, stock.Batch, stock.Quantity))
            .ToList();

        foreach (var stock in picked)
            stockRepository.Remove(stock);

        outbox.Enqueue(ToEnvelope(message.WaveId, lines));
        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: Message Translator (EIP) — stock keluar → integration event envelope (ADR-0005/0030)
    // How: EventId baru = identitas outbox/idempotency (Reporting dedup via Inbox atas EventId ini →
    // StockOnHandView decrement + DispatchSummary). Lines bisa kosong (wave tanpa Picked = dispatch sah).
    private static MessageEnvelope ToEnvelope(Guid waveId, IReadOnlyList<StockRemovedLineV1> lines)
    {
        var payload = new StockRemovedV1(waveId, lines);
        return new MessageEnvelope(
            EventId: Guid.NewGuid(),
            LogicalName: StockRemovedV1.LogicalName,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: JsonSerializer.Serialize(payload),
            Traceparent: null,
            Tracestate: null);
    }
}
