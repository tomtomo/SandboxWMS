using Wms.BuildingBlocks.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Warehouse; Add/GetById dari IRepository
public interface IWarehouseRepository : IRepository<Warehouse, WarehouseId>
{
}
