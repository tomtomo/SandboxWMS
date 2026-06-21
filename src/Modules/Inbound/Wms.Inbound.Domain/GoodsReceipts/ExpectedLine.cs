namespace Wms.Inbound.Domain;

// What: line PO yang di-snapshot ke GoodsReceipt — entity DI DALAM aggregate (ADR-0014)
// Why: expectedQty + uom dibekukan saat GR dibuka supaya makna dokumen historis tak berubah
// saat Product master di-update; konsistensinya dijaga GoodsReceipt sebagai satu-satunya entry point.
// How: setter privat, dikonstruksi hanya via GoodsReceipt; ctor parameterless untuk materialisasi EF.
public sealed class ExpectedLine
{
    public string Sku { get; private set; } = null!;

    public int ExpectedQty { get; private set; }

    public string Uom { get; private set; } = null!;

    private ExpectedLine() { }

    internal ExpectedLine(string sku, int expectedQty, string uom)
    {
        Sku = sku;
        ExpectedQty = expectedQty;
        Uom = uom;
    }
}
