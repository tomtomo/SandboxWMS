namespace Wms.Inbound.Domain;

// What: hasil hitung kuantitas per SKU (Value Object, transient — TIDAK dipersist)
// Why: overview menyebut quantityChecks[] sebagai field state Pending, tapi ia murni TURUNAN
// dari expectedLines vs scannedLines — dihitung on-demand, bukan disimpan (hindari state ganda
// yang bisa drift). Yang dipersist hanya discrepancies (gr_discrepancies). Dipakai UI Review +
// kompilasi discrepancy sumbu kuantitas.
public sealed record QuantityCheck(string Sku, int ExpectedQty, int ReceivedQty, QuantityVariance Variance);
