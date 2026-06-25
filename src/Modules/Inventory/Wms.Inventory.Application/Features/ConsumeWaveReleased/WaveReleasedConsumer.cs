using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Domain.Results;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Contracts;
using Wms.Inventory.Domain;
using Wms.Outbound.Contracts;

namespace Wms.Inventory.Application.Features.ConsumeWaveReleased;

// What: Idempotent consumer WaveReleased → alokasi FEFO QUANTITY-AWARE → emit StockAllocated (EIP; ADR-0005)
// Why: sisi Inventory dari §C3. Per line wave, penuhi qty yang DIMINTA dari Stock Available expiry-TERDEKAT
// (FEFO — First-Expired-First-Out): exhaust lot terdekat dulu, span ke lot berikut bila kurang, SPLIT lot
// terakhir bila melebihi sisa (porsi parsial → Stock baru Allocated, sisa tetap Available). Tanpa split →
// over-allocation (seluruh lot terkunci walau order < qty lot) = konservasi bocor + qty PickingTask salah.
// Setelah semua line, umumkan hasil via StockAllocatedV1 ke Outbound (buat PickingTask). STRATEGI FEFO =
// config INTERNAL Inventory: hanya HASIL alokasi (stock+qty terpilih) yang menyeberang kontrak, bukan
// algoritmanya (FIFO/LIFO/fixed-bin bisa menggantikan tanpa ubah kontrak). ACL: WaveReleasedV1 asing → model
// sendiri; StockAllocatedV1 di-emit.
// How: cek Inbox (eventId, HandlerType). Muat semua Stock Available untuk sku yang diminta SEKALI (tracked) →
// antrian per-sku urut FEFO → per line akumulasi: Peek lot; bila qty lot ≤ sisa → Dequeue + Allocate penuh
// (lanjut lot berikut); bila lot > sisa → SplitForAllocation (lot tetap di antrian, qty berkurang). In-memory
// queue cegah double-allocate stock sama dalam satu wave (mutasi belum terlihat query DB). Enqueue
// StockAllocated ke Outbox + AddAsync stock split + MarkProcessed + SaveChanges = SATU transaksi (anti dual-write).
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
        var shortLines = new List<StockAllocationFailedLineV1>();
        foreach (var line in message.Lines)
        {
            // Akumulasi qty lintas batch FEFO sampai qty LINE terpenuhi (bukan sekadar ambil 1 lot utuh):
            // lot expiry-terdekat di-exhaust DULU, lot terakhir yang MELEBIHI sisa di-SPLIT (porsi parsial).
            // Tanpa ini → alokasi seluruh lot pertama (over-allocation: mengunci stok tak dipesan + qty
            // PickingTask salah). SKU tanpa antrian (stock nol) → loop di-skip, remaining = qty penuh.
            var remaining = line.Qty;
            if (fefoQueues.TryGetValue(line.Sku, out var queue))
                while (remaining > 0 && queue.Count > 0)
                {
                    var stock = queue.Peek();
                    if (stock.Quantity <= remaining)
                    {
                        // lot habis dialokasi PENUH → keluar antrian, lanjut lot FEFO berikut untuk sisa
                        queue.Dequeue();
                        var allocate = stock.Allocate(message.WaveId);
                        if (allocate.IsFailure)
                            return Result.Failure(allocate.Error);

                        allocations.Add(new StockAllocationV1(
                            line.OrderId, stock.Sku, stock.LocationId, stock.Batch, stock.Quantity, stock.Id.Value));
                        remaining -= stock.Quantity;
                    }
                    else
                    {
                        // lot > sisa → SPLIT: alokasi `remaining` ke Stock baru; lot ini tetap Available dgn
                        // qty berkurang (tetap di antrian untuk line berikutnya sku yang sama). Konservasi terjaga.
                        var split = stock.SplitForAllocation(StockId.New(), remaining, message.WaveId);
                        if (split.IsFailure)
                            return Result.Failure(split.Error);

                        var allocated = split.Value;
                        await stockRepository.AddAsync(allocated, cancellationToken);
                        allocations.Add(new StockAllocationV1(
                            line.OrderId, allocated.Sku, allocated.LocationId, allocated.Batch, allocated.Quantity, allocated.Id.Value));
                        remaining = 0;
                    }
                }

            // ADR-0034: line tak teralokasi penuh (stock kurang/nol) → catat short EKSPLISIT (ganti silent-drop).
            // allocatedQty + shortQty == requestedQty (konservasi). allocatedQty 0 = sama sekali tak ada stock.
            if (remaining > 0)
                shortLines.Add(new StockAllocationFailedLineV1(
                    line.OrderId, line.Sku, line.Qty, line.Qty - remaining, remaining));
        }

        outbox.Enqueue(ToEnvelope(message.WaveId, allocations));
        // ADR-0034: emit sinyal-gagal HANYA bila ada line short (hindari event kosong) → Outbound tandai
        // OrderLine Short/Backordered + Notification alert. Satu transaksi dgn alokasi + Inbox-mark.
        if (shortLines.Count > 0)
            outbox.Enqueue(ToFailedEnvelope(message.WaveId, shortLines));
        inbox.MarkProcessed(eventId, HandlerType);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // What: Message Translator (EIP) — hasil alokasi → integration event envelope (ADR-0005)
    // How: EventId baru = identitas outbox/idempotency (consumer Outbound dedup via Inbox atas EventId ini).
    private static MessageEnvelope ToEnvelope(Guid waveId, IReadOnlyList<StockAllocationV1> allocations)
    {
        var payload = new StockAllocatedV1(waveId, allocations);
        return MessageEnvelope.For(StockAllocatedV1.LogicalName, payload);
    }

    // What: Message Translator (EIP) — line short → StockAllocationFailed envelope (ADR-0034)
    // How: EventId baru = identitas outbox/idempotency; konsumen (Outbound + Notification) dedup via Inbox.
    private static MessageEnvelope ToFailedEnvelope(Guid waveId, IReadOnlyList<StockAllocationFailedLineV1> shortLines)
    {
        var payload = new StockAllocationFailedV1(waveId, shortLines);
        return MessageEnvelope.For(StockAllocationFailedV1.LogicalName, payload);
    }
}
