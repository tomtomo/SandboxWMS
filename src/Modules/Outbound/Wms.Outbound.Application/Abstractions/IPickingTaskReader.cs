using Wms.BuildingBlocks.Application.Pagination;
using Wms.Outbound.Application.ReadModels;

namespace Wms.Outbound.Application.Abstractions;

// What: Read-Port (CQRS read-side + Ports & Adapters; ADR-0004/0011) — sisi-baca PickingTask
// Why: papan kerja operator butuh daftar task ter-filter (assignedTo/status/waveId). Reader DEDIKASI
// (bukan reuse IPickingTaskRepository tracked/unpaged write-side); endpoint REST bergantung abstraksi
// ini, BUKAN DbContext (FF#8). Paginated (PagedResult) — cegah unbounded result set.
// How: ListAsync dengan conditional filter per argumen non-null; page 1-based.
public interface IPickingTaskReader
{
    Task<PagedResult<PickingTaskReadModel>> ListAsync(
        string? assignedTo = null,
        string? status = null,
        Guid? waveId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
