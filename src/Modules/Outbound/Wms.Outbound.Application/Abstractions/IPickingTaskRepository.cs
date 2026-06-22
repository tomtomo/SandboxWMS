using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port PickingTask; Add/GetById dari IRepository + query domain
public interface IPickingTaskRepository : IRepository<PickingTask, PickingTaskId>
{
    // What: semua PickingTask terikat ke wave (CompletePicking → gate Wave→Ready: cek semua Completed)
    Task<IReadOnlyList<PickingTask>> ListByWaveAsync(Guid waveId, CancellationToken cancellationToken = default);
}
