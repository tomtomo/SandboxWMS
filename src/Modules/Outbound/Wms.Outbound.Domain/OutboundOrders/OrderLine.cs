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

    private OrderLine() { }

    internal OrderLine(string sku, int qty, string uom)
    {
        Sku = sku;
        Qty = qty;
        Uom = uom;
    }
}
