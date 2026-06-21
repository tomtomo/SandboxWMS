using System.Text.Json;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Contracts;
using Wms.Inventory.Domain;
using Wms.Outbound.Contracts;

namespace Wms.Inventory.Application.Features.ConsumeWaveReleased;

// What: Idempotent consumer WaveReleased → alokasi FEFO → emit StockAllocated (EIP; ADR-0005)
// Why: sisi Inventory dari §C3. Per line wave, pilih Stock Available dengan expiry TERDEKAT
// (FEFO — First-Expired-First-Out) lalu mark Allocated ke wave; setelah semua line, umumkan hasil
// via StockAllocatedV1 ke Outbound (buat PickingTask). STRATEGI FEFO = config INTERNAL Inventory:
// hanya HASIL alokasi yang menyeberang kontrak, bukan algoritmanya (FIFO/LIFO/fixed-bin bisa
// menggantikan tanpa ubah kontrak). ACL: WaveReleasedV1 asing → model sendiri; StockAllocatedV1 di-emit.
// How: cek Inbox (eventId, HandlerType). Muat semua Stock Available untuk sku yang diminta SEKALI
// (tracked) → antrian per-sku urut FEFO → dequeue per line (cegah double-allocate stock sama dalam satu
// wave; perubahan in-memory belum terlihat query DB). stock.Allocate(waveId) (transisi + guard di domain).
// Enqueue StockAllocated ke Outbox + MarkProcessed + SaveChanges = SATU transaksi (anti dual-write).
public sealed class WaveReleasedConsumer(
    IStockRepository stockRepository,
    IIntegrationEventOutbox outbox,
    IInboxGuard inbox,
    IUnitOfWork unitOfWork)
{
    // identitas handler untuk composite inbox key (event_id, handler_type) — ADR-0005
    public const string HandlerType = "inventory.wave-released";

    public async Task<Result> HandleAsync(
        Guid eventId, WaveReleasedV1 message, CancellationToken cancellationToken = default)
    {
        if (await inbox.HasProcessedAsync(eventId, HandlerType, cancellationToken))
            return Result.Success();

        var skus = message.Lines.Select(line => line.Sku).Distinct().ToArray();
        var available = await stockRepository.ListAvailableBySkusAsync(skus, cancellationToken);

        // FEFO: per sku, antrian batch urut expiry terdekat (null = paling akhir), tiebreak stabil by id.
        var fefoQueues = available
            .GroupBy(stock => stock.Sku)
            .ToDictionary(
                group => group.Key,
                group => new Queue<Stock>(
                    group.OrderBy(stock => stock.Expiry ?? DateOnly.MaxValue).ThenBy(stock => stock.Id.Value)));

        var allocations = new List<StockAllocationV1>();
        foreach (var line in message.Lines)
        {
            // Stock Available tak cukup = allocation failure → OUT-OF-SCOPE global (assume cukup): tak
            // membangun partial/reject/notify policy; line tak terpenuhi sekadar tak menghasilkan alokasi.
            if (!fefoQueues.TryGetValue(line.Sku, out var queue) || queue.Count == 0)
                continue;

            var stock = queue.Dequeue();
            var allocate = stock.Allocate(message.WaveId);
            if (allocate.IsFailure)
                return Result.Failure(allocate.Error);

            allocations.Add(new StockAllocationV1(
                stock.Sku, stock.LocationId, stock.Batch, stock.Quantity, stock.Id.Value));
        }

        outbox.Enqueue(ToEnvelope(message.WaveId, allocations));
        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: Message Translator (EIP) — hasil alokasi → integration event envelope (ADR-0005)
    // How: EventId baru = identitas outbox/idempotency (consumer Outbound dedup via Inbox atas EventId ini).
    private static MessageEnvelope ToEnvelope(Guid waveId, IReadOnlyList<StockAllocationV1> allocations)
    {
        var payload = new StockAllocatedV1(waveId, allocations);
        return new MessageEnvelope(
            EventId: Guid.NewGuid(),
            LogicalName: StockAllocatedV1.LogicalName,
            OccurredAt: DateTimeOffset.UtcNow,
            Payload: JsonSerializer.Serialize(payload),
            Traceparent: null,
            Tracestate: null);
    }
}
