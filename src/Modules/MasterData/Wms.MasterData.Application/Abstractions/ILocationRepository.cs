using Wms.BuildingBlocks.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Location; Add/GetById dari IRepository
public interface ILocationRepository : IRepository<Location, LocationId>
{
}
