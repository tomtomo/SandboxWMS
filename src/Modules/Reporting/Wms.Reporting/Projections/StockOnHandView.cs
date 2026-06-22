namespace Wms.Reporting.Projections;

// What: Read-model / Projection (CQRS read-side; ADR-0017) — stok on-hand per (warehouse, sku, batch)
// Why: denormalized snapshot untuk inventory dashboard (overview §F). BUKAN aggregate — tak punya domain
// invariant (cermin event core); validasi ada di write-side yang meng-emit event. Di-build via eventual
// consistency: GRConfirmed → Receive(+); StockRemoved (dispatch) → Remove(−). Rebuild-able dari event.
// How: PK natural komposit (WarehouseId, Sku, Batch). Batch NON-NULL ("" = produk tanpa batch-tracking)
// supaya layak jadi bagian PK (find-or-create-by-PK, ADR-0017). Mutasi via method; store yang find-or-create.
public sealed class StockOnHandView
{
    public string WarehouseId { get; private set; } = null!;

    public string Sku { get; private set; } = null!;

    // "" = no-batch (produk tanpa batch-tracking) — PK tak boleh null
    public string Batch { get; private set; } = null!;

    public int QtyOnHand { get; private set; }

    private StockOnHandView() { }

    public StockOnHandView(string warehouseId, string sku, string batch)
    {
        WarehouseId = warehouseId;
        Sku = sku;
        Batch = batch;
    }

    public void Receive(int qty) => QtyOnHand += qty;

    public void Remove(int qty) => QtyOnHand -= qty;
}
