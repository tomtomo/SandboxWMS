using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.MasterData.Application.Features.CreateWarehouse;

// What: CQRS Command (ADR-0004) — daftar Warehouse master baru (overview §D)
// Why: write-intent → Result<Guid> (warehouseId baru) sebagai nilai (no-throw, ADR-0019).
public sealed record CreateWarehouseCommand(string Name, string Address) : ICommand<Guid>;
