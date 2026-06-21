namespace Wms.Inbound.Domain;

// What: input record factory GoodsReceipt.Create (bukan bagian state aggregate)
// Why: pisahkan data mentah pemanggil dari entity GoodsReceiptLine yang
// dikonstruksi & divalidasi DI DALAM aggregate — invariant lewat satu pintu.
public sealed record GoodsReceiptLineInput(string Sku, int Quantity);
