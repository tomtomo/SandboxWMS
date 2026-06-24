using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Inventory.Domain;

// What: Aggregate Root (DDD) — Stock, balance fisik per (sku, location, batch) dgn lifecycle penuh
// Why: satu-satunya entry point konsistensi untuk transisi state-nya. Overview §B mendefinisikan
// siklus Quarantine/OnHand → Available (putaway) → Allocated (wave) → Picked (picking) → removed
// (dispatch). Tiap transisi dipicu trigger BERBEDA (REST putaway; event WaveReleased/PickingCompleted/
// ShipmentDispatched) — tapi LEGALITAS-nya ditegakkan di sini, bukan di consumer: state machine milik
// aggregate. Prasyarat salah → Result.Failure (no-throw, FF#7); state tak berubah.
// How: factory CreateOnHand/CreateQuarantine memvalidasi → Result<Stock>; method transisi memeriksa
// state-sekarang lalu memutasi field + Status. IAuditable via AuditableAggregateRoot (created_by=SYSTEM
// saat consumer mesin membuatnya). Tak me-raise domain event: StockAllocated lahir di level wave
// (mengagregasi banyak Stock) → dikomposisi consumer WaveReleased, bukan event per-aggregate.
public sealed class Stock : AuditableAggregateRoot<StockId>
{
    public string WarehouseId { get; private set; } = null!;

    public string Sku { get; private set; } = null!;

    // What: lokasi fisik — BERUBAH sepanjang lifecycle (receiving/quarantine → rack → staging)
    public string LocationId { get; private set; } = null!;

    public string? Batch { get; private set; }

    public DateOnly? Expiry { get; private set; }

    public int Quantity { get; private set; }

    public Guid SourceGoodsReceiptId { get; private set; }

    public StockStatus Status { get; private set; }

    // What: wave yang mereservasi stock ini (set saat Allocate) — cegah double-allocate
    public Guid? AllocatedToWaveId { get; private set; }

    // What: PickingTask yang mengambil stock ini (set saat Pick) — realisasi Stock.Picked (ADR-0028)
    public Guid? PickingTaskId { get; private set; }

    private Stock() { }

    private Stock(
        StockId id, string warehouseId, string sku, string locationId,
        string? batch, DateOnly? expiry, int quantity, Guid sourceGoodsReceiptId, StockStatus status)
        : base(id)
    {
        WarehouseId = warehouseId;
        Sku = sku;
        LocationId = locationId;
        Batch = batch;
        Expiry = expiry;
        Quantity = quantity;
        SourceGoodsReceiptId = sourceGoodsReceiptId;
        Status = status;
    }

    // What: factory — stock baru dari receivedLine Good (overview §B1) → state OnHand di receiving area
    // Why: belum di rak → belum available untuk alokasi; memicu PutawayTask (di consumer).
    public static Result<Stock> CreateOnHand(
        StockId id, string warehouseId, string sku, string locationId,
        string? batch, DateOnly? expiry, int quantity, Guid sourceGoodsReceiptId)
        => Create(id, warehouseId, sku, locationId, batch, expiry, quantity, sourceGoodsReceiptId, StockStatus.OnHand);

    // What: factory — stock baru dari receivedLine QcHold (overview §B1) → state Quarantine
    // Why: barang ditahan QC di quarantine area; TAK available & TAK generate PutawayTask (tak masuk
    // rak reguler). Release QC→OnHand = out-of-scope global.
    public static Result<Stock> CreateQuarantine(
        StockId id, string warehouseId, string sku, string locationId,
        string? batch, DateOnly? expiry, int quantity, Guid sourceGoodsReceiptId)
        => Create(id, warehouseId, sku, locationId, batch, expiry, quantity, sourceGoodsReceiptId, StockStatus.Quarantine);

    private static Result<Stock> Create(
        StockId id, string warehouseId, string sku, string locationId,
        string? batch, DateOnly? expiry, int quantity, Guid sourceGoodsReceiptId, StockStatus status)
    {
        if (string.IsNullOrWhiteSpace(warehouseId))
            return Result.Failure<Stock>(StockErrors.MissingWarehouse);
        if (string.IsNullOrWhiteSpace(sku))
            return Result.Failure<Stock>(StockErrors.MissingSku);
        if (string.IsNullOrWhiteSpace(locationId))
            return Result.Failure<Stock>(StockErrors.MissingLocation);
        if (quantity <= 0)
            return Result.Failure<Stock>(StockErrors.NonPositiveQuantity);

        return Result.Success(
            new Stock(id, warehouseId, sku, locationId, batch, expiry, quantity, sourceGoodsReceiptId, status));
    }

    // What: transisi OnHand → Available (overview §B2, PutawayTask completed)
    // Why: barang dipindah dari receiving area ke rak → kini free untuk dialokasi ke wave.
    public Result Putaway(string rackLocationId)
    {
        if (Status != StockStatus.OnHand)
            return Result.Failure(StockErrors.InvalidPutaway);
        if (string.IsNullOrWhiteSpace(rackLocationId))
            return Result.Failure(StockErrors.MissingLocation);

        LocationId = rackLocationId;
        Status = StockStatus.Available;
        return Result.Success();
    }

    // What: transisi Available → Allocated (overview §C3, dipicu WaveReleased)
    // Why: stock direservasi ke wave tertentu (tetap fisik di rak); cegah double-allocate via guard state.
    public Result Allocate(Guid waveId)
    {
        if (Status != StockStatus.Available)
            return Result.Failure(StockErrors.InvalidAllocation);

        AllocatedToWaveId = waveId;
        Status = StockStatus.Allocated;
        return Result.Success();
    }

    // What: koreksi manual kuantitas (set absolut → newQty), DI LUAR siklus receive→pick overview §B
    // Why: balance fisik bisa menyimpang dari sistem (cycle count, kerusakan, salah-hitung) → operator
    // mengoreksi langsung. SENGAJA minimal: tak ada guard state (berlaku di state apa pun) & tak meng-emit
    // event (tak ada konsumen downstream terdokumentasi). Satu invariant: kuantitas tak boleh negatif
    // (Result.Failure, no-throw FF#7) — state tak berubah saat gagal.
    public Result Adjust(int newQty)
    {
        if (newQty < 0)
            return Result.Failure(StockErrors.NegativeQuantity);

        Quantity = newQty;
        return Result.Success();
    }

    // What: transisi Allocated → Picked (overview §C5, dipicu PickingCompleted — ADR-0028)
    // Why: barang diambil dari rak ke staging area; menyimpan pickingTaskId + lokasi staging.
    public Result Pick(Guid pickingTaskId, string stagingLocationId)
    {
        if (Status != StockStatus.Allocated)
            return Result.Failure(StockErrors.InvalidPick);
        if (string.IsNullOrWhiteSpace(stagingLocationId))
            return Result.Failure(StockErrors.MissingLocation);

        PickingTaskId = pickingTaskId;
        LocationId = stagingLocationId;
        Status = StockStatus.Picked;
        return Result.Success();
    }
}
