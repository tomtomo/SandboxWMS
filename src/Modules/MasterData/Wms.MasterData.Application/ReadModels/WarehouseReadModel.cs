namespace Wms.MasterData.Application.ReadModels;

// What: read DTO Warehouse (CQRS read-side; ADR-0004 / ADR-0011) — dipublish read-API gRPC + di-cache
// Why: record immutable ringan untuk sisi query; aman di-cache. Hanya warehouse aktif yang dilayani.
public sealed record WarehouseReadModel(
    Guid WarehouseId,
    string Name,
    string Address);
