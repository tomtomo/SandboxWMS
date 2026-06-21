using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.ReadModels;

// What: read DTO Location (CQRS read-side; ADR-0004 / ADR-0011) — dipublish read-API gRPC + di-cache
// Why: record immutable ringan untuk sisi query; aman di-cache. Type=LocationType (domain enum) dibawa
// agar konsumen tahu peran lokasi (receiving/rack/quarantine/staging). Hanya location aktif dilayani.
public sealed record LocationReadModel(
    Guid LocationId,
    Guid WarehouseId,
    LocationType Type,
    string Code);
