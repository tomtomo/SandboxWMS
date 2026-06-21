namespace Wms.Outbound.Domain;

// What: input record factory OutboundOrder.Create (snapshot order line; bukan bagian state aggregate)
// Why: pisahkan data mentah pemanggil dari entity OrderLine yang dikonstruksi & divalidasi DI DALAM
// aggregate — invariant lewat satu pintu. Sku/Qty/Uom = snapshot kritikal (ADR-0014: uom dibekukan
// saat order masuk agar dokumen historis stabil walau Product master berubah).
public sealed record OrderLineInput(string Sku, int Qty, string Uom);
