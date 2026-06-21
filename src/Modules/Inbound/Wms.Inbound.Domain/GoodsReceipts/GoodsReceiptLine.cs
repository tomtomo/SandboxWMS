namespace Wms.Inbound.Domain;

// What: line item GoodsReceipt — entity DI DALAM aggregate (bukan aggregate root)
// Why: line tak punya lifecycle lintas-aggregate; konsistensinya dijaga GoodsReceipt
// sebagai satu-satunya entry point (DDD aggregate boundary).
// How: setter privat, hanya dikonstruksi via GoodsReceipt; ctor parameterless untuk
// materialisasi EF.
public sealed class GoodsReceiptLine
{
    public string Sku { get; private set; } = null!;

    public int Quantity { get; private set; }

    private GoodsReceiptLine() { }

    internal GoodsReceiptLine(string sku, int quantity)
    {
        Sku = sku;
        Quantity = quantity;
    }
}
