namespace Wms.Outbound.Domain;

// What: line order yang di-snapshot ke OutboundOrder — entity DI DALAM aggregate (ADR-0014)
// Why: qty + uom dibekukan saat order masuk WMS supaya makna dokumen historis tak berubah saat Product
// master di-update; konsistensinya dijaga OutboundOrder sebagai satu-satunya entry point. WaveReleased
// lines[] (per-line orderId/sku/qty) dikomposisi dari kumpulan OrderLine lintas-order saat CreateWave.
// How: setter privat, dikonstruksi hanya via OutboundOrder; ctor parameterless untuk materialisasi EF.
public sealed class OrderLine
{
    public string Sku { get; private set; } = null!;

    public int Qty { get; private set; }

    public string Uom { get; private set; } = null!;

    // What: hasil alokasi line (ADR-0034) — default Pending sampai wave allocation me-resolve
    public OrderLineAllocationStatus AllocationStatus { get; private set; } = OrderLineAllocationStatus.Pending;

    private OrderLine() { }

    internal OrderLine(string sku, int qty, string uom)
    {
        Sku = sku;
        Qty = qty;
        Uom = uom;
    }

    // What: tandai teralokasi (StockAllocated). Hanya promote dari Pending — TAK menimpa Short (Short menang
    // bila line teralokasi sebagian: ordering event eventual tak menentukan hasil akhir). Idempotent.
    internal void MarkAllocated()
    {
        if (AllocationStatus == OrderLineAllocationStatus.Pending)
            AllocationStatus = OrderLineAllocationStatus.Allocated;
    }

    // What: tandai short/backorder (StockAllocationFailed) — selalu menang (line butuh perhatian). Idempotent.
    internal void MarkShort() => AllocationStatus = OrderLineAllocationStatus.Short;
}
