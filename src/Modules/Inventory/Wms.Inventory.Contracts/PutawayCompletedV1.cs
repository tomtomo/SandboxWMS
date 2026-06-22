namespace Wms.Inventory.Contracts;

// What: Integration Event (Published Language; ADR-0005 / ADR-0009 / ADR-0030) — Inventory → Reporting
// Why: overview §F mewajibkan OperatorActivity menghitung PUTAWAY COUNT per operator, tapi penyelesaian
// putaway (PutawayTask Assigned→Completed, overview §B2) tak punya kanal lintas-context — gap yang
// direalisasikan ADR-0030 (pola sama ADR-0028). Karena PutawayTask & Stock milik Inventory, INVENTORI
// yang mengumumkan fakta ini (pemilik data; DB-per-service ADR-0010). Pure read-side consumer (Reporting)
// tak boleh sync-query core (ADR-0017) → state ter-bawa di event.
// How: record immutable, POCO ZERO transport dep (ADR-0009). Di-emit handler CompletePutaway dalam SATU
// transaksi dgn transisi PutawayTask/Stock (anti dual-write). operatorId = aktor penyelesai (ICurrentUser,
// SYSTEM s/d authZ 07a — ADR-0012/0027). LogicalName terdaftar di asyncapi.yaml (FF#11).
public sealed record PutawayCompletedV1(
    Guid PutawayTaskId,
    Guid StockId,
    string Sku,
    string WarehouseId,
    string? OperatorId)
{
    public const string LogicalName = "inventory.putaway_completed.v1";
}
