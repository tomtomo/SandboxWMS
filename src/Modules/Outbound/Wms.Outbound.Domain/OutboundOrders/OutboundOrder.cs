using Wms.BuildingBlocks.Domain.Primitives;
using Wms.BuildingBlocks.Domain.Results;

namespace Wms.Outbound.Domain;

// What: Aggregate Root (DDD) — OutboundOrder: order pengiriman pelanggan, multi-SKU (overview §C)
// Why: satu-satunya entry point konsistensi siklus order. New saat masuk WMS (orderLines di-snapshot,
// ADR-0014); InProgress saat dimasukkan ke wave (CreateWave); Closed saat wave dispatch. Tiap transisi
// menegakkan prasyarat state via Result (no-throw, FF#7); state tak berubah saat guard gagal. Tak me-raise
// domain event: WaveReleased mengagregasi lines LINTAS-order → dikomposisi handler CreateWave (seperti
// StockAllocated di Inventory), bukan event per-aggregate. ShipmentDispatched dimiliki Wave.
// How: factory Create memvalidasi customer/shipTo + tiap line → Result<OutboundOrder>; orderLines = owned
// collection (OrderLine entity). IAuditable via AuditableAggregateRoot (created_by operator REST).
public sealed class OutboundOrder : AuditableAggregateRoot<OutboundOrderId>
{
    private readonly List<OrderLine> _orderLines = new();

    public string CustomerId { get; private set; } = null!;

    public string ShipTo { get; private set; } = null!;

    // What: wave yang memproses order ini (set saat PlaceInWave) — back-reference ke Wave aggregate by id
    public Guid? WaveId { get; private set; }

    public OutboundOrderStatus Status { get; private set; }

    public IReadOnlyCollection<OrderLine> OrderLines => _orderLines.AsReadOnly();

    private OutboundOrder() { }

    private OutboundOrder(OutboundOrderId id, string customerId, string shipTo)
        : base(id)
    {
        CustomerId = customerId;
        ShipTo = shipTo;
        Status = OutboundOrderStatus.New;
    }

    // What: factory + invariant guard (Result pattern, ADR-0019)
    // Why: order eksternal masuk dgn orderLines di-SNAPSHOT (sku/qty/uom) — uom dibekukan agar dokumen
    // historis stabil saat Product master berubah (ADR-0014). Sampai MasterData (04a), snapshot disuplai
    // pemanggil sebagai stand-in (seed). Validasi shape minimal; order langsung state New.
    public static Result<OutboundOrder> Create(
        OutboundOrderId id,
        string customerId,
        string shipTo,
        IReadOnlyCollection<OrderLineInput> orderLines)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            return Result.Failure<OutboundOrder>(OutboundOrderErrors.MissingCustomer);
        if (string.IsNullOrWhiteSpace(shipTo))
            return Result.Failure<OutboundOrder>(OutboundOrderErrors.MissingShipTo);
        if (orderLines.Count == 0)
            return Result.Failure<OutboundOrder>(OutboundOrderErrors.NoOrderLines);

        foreach (var line in orderLines)
        {
            if (string.IsNullOrWhiteSpace(line.Sku))
                return Result.Failure<OutboundOrder>(OutboundOrderErrors.MissingSku);
            if (line.Qty <= 0)
                return Result.Failure<OutboundOrder>(OutboundOrderErrors.NonPositiveQuantity);
            if (string.IsNullOrWhiteSpace(line.Uom))
                return Result.Failure<OutboundOrder>(OutboundOrderErrors.MissingUom);
        }

        var order = new OutboundOrder(id, customerId, shipTo);
        foreach (var line in orderLines)
            order._orderLines.Add(new OrderLine(line.Sku, line.Qty, line.Uom));

        return Result.Success(order);
    }

    // What: transisi New → InProgress (overview §C2, dipicu CreateWave)
    // Why: order dimasukkan ke wave aktif; back-reference waveId di-set. Cegah double-wave via guard state.
    public Result PlaceInWave(Guid waveId)
    {
        if (Status != OutboundOrderStatus.New)
            return Result.Failure(OutboundOrderErrors.InvalidWaveAssignment);

        WaveId = waveId;
        Status = OutboundOrderStatus.InProgress;
        return Result.Success();
    }

    // What: transisi InProgress → Closed (overview §C6, dipicu DispatchWave)
    // Why: wave sudah dispatch (barang keluar) → order selesai dari sisi WMS. Terminal.
    public Result Close()
    {
        if (Status != OutboundOrderStatus.InProgress)
            return Result.Failure(OutboundOrderErrors.InvalidClose);

        Status = OutboundOrderStatus.Closed;
        return Result.Success();
    }

    // What: tandai status alokasi sebuah line (ADR-0034) — dipicu consumer StockAllocated/StockAllocationFailed.
    // Why: aggregate = satu-satunya entry point mutasi line; presedensi Short>Allocated dijaga OrderLine. No-op
    // bila sku tak ada (idempotent + defensif vs payload tak sinkron) — tak melanggar invariant, tak butuh Result.
    public void MarkLineAllocated(string sku) => LineFor(sku)?.MarkAllocated();

    public void MarkLineShort(string sku) => LineFor(sku)?.MarkShort();

    // orderLines[] = satu line per sku (overview §C) → match unik by sku
    private OrderLine? LineFor(string sku) =>
        _orderLines.FirstOrDefault(line => line.Sku == sku);
}
