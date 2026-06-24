namespace Wms.MasterData.Application.ReadModels;

// What: read DTO list-item Warehouse (CQRS read-side; ADR-0004 / ADR-0011) — dipakai REST list-API (UI)
// Why: SEPARATE dari WarehouseReadModel (kontrak gRPC by-id, tanpa IsActive). List-API manajemen membawa
// IsActive agar UI bisa memfilter & menandai gudang non-aktif. record immutable, snapshot ringan.
public sealed record WarehouseListItem(
    Guid WarehouseId,
    string Name,
    string Address,
    bool IsActive);
