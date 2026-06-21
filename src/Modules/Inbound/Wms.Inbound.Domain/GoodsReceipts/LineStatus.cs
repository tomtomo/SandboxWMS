namespace Wms.Inbound.Domain;

// What: sumbu KONDISI line (operator-tagged) dari two-axis discrepancy (ADR-0013)
// Why: kondisi item di-tag manual operator saat scan — independen dari sumbu kuantitas
// (QuantityVariance). Good = normal; WrongItem = barang salah (→ rejected); QcHold = perlu
// inspeksi QC (→ Quarantine di Inventory).
public enum LineStatus
{
    Good,
    WrongItem,
    QcHold
}
