namespace Wms.Inbound.Domain;

// What: hasil satu scan carton/line — entity DI DALAM aggregate GoodsReceipt
// Why: tiap scan menangkap kuantitas fisik + kondisi (lineStatus) + batch/expiry; konsistensinya
// dijaga GoodsReceipt (append hanya saat InProgress). Bukan aggregate root — tak punya lifecycle
// lintas-aggregate sendiri.
// How: setter privat, dikonstruksi hanya via GoodsReceipt.ScanItem; ctor parameterless untuk EF.
public sealed class ScannedLine
{
    public string Sku { get; private set; } = null!;

    public int ActualQty { get; private set; }

    public string? Batch { get; private set; }

    public DateOnly? Expiry { get; private set; }

    public LineStatus LineStatus { get; private set; }

    private ScannedLine() { }

    internal ScannedLine(string sku, int actualQty, string? batch, DateOnly? expiry, LineStatus lineStatus)
    {
        Sku = sku;
        ActualQty = actualQty;
        Batch = batch;
        Expiry = expiry;
        LineStatus = lineStatus;
    }
}
