namespace Wms.Inbound.Domain;

// What: sumbu KUANTITAS (system-computed) dari two-axis discrepancy (ADR-0013)
// Why: selisih qty dihitung sistem saat scan selesai — independen dari sumbu kondisi
// (LineStatus). Membandingkan total actual (Good+QcHold) vs expectedQty per SKU.
public enum QuantityVariance
{
    Normal,
    ShortDelivery,
    OverDelivery
}
