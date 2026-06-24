namespace Wms.MasterData.Application.ReadModels;

// What: read DTO list-item Location (CQRS read-side; ADR-0004 / ADR-0011) — dipakai REST list-API (UI)
// Why: SEPARATE dari LocationReadModel (kontrak gRPC by-id, tanpa IsActive; Type=enum). List-API manajemen
// membawa IsActive (filter/badge UI) dan men-FLATTEN Type ke STRING (LocationType.ToString()) — konsumen UI
// menerima nama enum stabil tanpa bergantung JsonStringEnumConverter di host. record immutable, snapshot ringan.
public sealed record LocationListItem(
    Guid LocationId,
    Guid WarehouseId,
    string Type,
    string Code,
    bool IsActive);
