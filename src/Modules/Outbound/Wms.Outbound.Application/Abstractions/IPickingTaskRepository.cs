using Wms.Outbound.Domain;

namespace Wms.Outbound.Application.Abstractions;

// What: Repository Pattern (DDD) — port PickingTask (impl EF di Infrastructure)
public interface IPickingTaskRepository
{
    Task AddAsync(PickingTask task, CancellationToken cancellationToken = default);

    Task<PickingTask?> GetAsync(PickingTaskId id, CancellationToken cancellationToken = default);

    // What: semua PickingTask terikat ke wave (CompletePicking → gate Wave→Ready: cek semua Completed)
    Task<IReadOnlyList<PickingTask>> ListByWaveAsync(Guid waveId, CancellationToken cancellationToken = default);
}
