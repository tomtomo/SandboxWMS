namespace Wms.Outbound.Domain;

// What: status alokasi per OrderLine (ADR-0034) — hasil pencocokan demand vs supply di wave allocation
// Why: order capture decoupled dari availability (eShop pattern) → tiap line punya NASIB eksplisit alih-alih
// silent-drop. Pending (default, belum di-resolve) → Allocated (teralokasi penuh) atau Short (stock kurang/nol,
// butuh perhatian/backorder). Short MENANG atas Allocated bila line teralokasi sebagian (lihat OutboundOrder).
public enum OrderLineAllocationStatus
{
    Pending = 0,
    Allocated = 1,
    Short = 2,
}
