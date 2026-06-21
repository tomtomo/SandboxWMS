namespace Wms.Inbound.Domain;

// What: input record factory GoodsReceipt.Create (snapshot PO line; bukan bagian state aggregate)
// Why: pisahkan data mentah pemanggil dari entity ExpectedLine yang dikonstruksi & divalidasi
// DI DALAM aggregate — invariant lewat satu pintu. Sku/ExpectedQty/Uom = snapshot kritikal (ADR-0014).
public sealed record ExpectedLineInput(string Sku, int ExpectedQty, string Uom);
