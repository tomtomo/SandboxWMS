using Wms.BuildingBlocks.Application.Messaging;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Features.CreateLocation;

// What: CQRS Command (ADR-0004) — daftar Location master baru di dalam Warehouse (overview §D)
// Why: write-intent → Result<Guid> (locationId baru). WarehouseId (Guid) = referensi by-id ke
// Warehouse; Type=LocationType menentukan peran lokasi di core flow.
public sealed record CreateLocationCommand(Guid WarehouseId, LocationType Type, string Code) : ICommand<Guid>;
