namespace Wms.Inventory.Application.Abstractions;

// What: Port (Anti-Corruption Layer; ADR-0011) — resolve kode lokasi default via MasterData read-API
// Why: Inventory menempatkan Stock di receiving/quarantine area saat GRConfirmed (overview §B). Lokasi
// default per PERAN diakses via gRPC read-API MasterData (GetDefaultLocation by warehouse+type) DI BALIK
// port core-neutral ini — consumer tak tahu gRPC (Hexagonal). ACL: adapter menerjemahkan LocationKind
// Inventory → proto LocationType, dan LocationReply → kode string. null = tak ada lokasi tipe itu (consumer
// loud-fail → DLQ). MENGGANTIKAN seed konstanta InventoryLocations (Phase 04a follow-up).
public interface ILocationCatalog
{
    Task<string?> GetDefaultLocationCodeAsync(
        string warehouseId, LocationKind kind, CancellationToken cancellationToken = default);
}

// What: peran lokasi yang dikonsumsi Inventory (model ACL SENDIRI — bukan tipe domain/proto MasterData)
// Why: ACL — Inventory tak meminjam MasterData.Domain.LocationType / proto enum; adapter yang translate.
public enum LocationKind
{
    ReceivingArea,
    QuarantineArea,
}
