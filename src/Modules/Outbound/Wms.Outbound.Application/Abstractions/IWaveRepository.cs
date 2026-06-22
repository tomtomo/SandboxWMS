using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port Wave; Add/GetById dari IRepository
public interface IWaveRepository : IRepository<Wave, WaveId>
{
}
