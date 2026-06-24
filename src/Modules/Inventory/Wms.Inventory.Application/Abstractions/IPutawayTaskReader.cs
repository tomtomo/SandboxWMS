using Wms.BuildingBlocks.Application.Pagination;
using Wms.Inventory.Application.ReadModels;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Application.Abstractions;

// What: Read-Port (CQRS read-side + Ports & Adapters; ADR-0004/0011) — sisi-baca PutawayTask
// Why: query list mem-bypass aggregate/repository (baca langsung → read-DTO) sesuai CQRS; endpoint
// REST (*.Api) bergantung ke abstraksi ini, BUKAN DbContext (FF#8) — impl EF di Infrastructure.
// How: dipanggil ListPutawayTasksEndpoint; assignedTo/status optional filter scoping; page 1-based.
public interface IPutawayTaskReader
{
    Task<PagedResult<PutawayTaskListItem>> ListAsync(
        string? assignedTo = null,
        PutawayTaskStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}
