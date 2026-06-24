using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Application.ReadModels;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IPutawayTaskReader
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8) — query list dilayani di sini, AsNoTracking
// (read murni), paginated (Skip/Take) + TotalCount (CountAsync) atas FILTER yang SAMA. Materialize-then-map
// (Status enum→string & StockId→Guid in-memory, bebas batasan translasi strongly-typed id).
// How: clamp page/pageSize (guard) → 2 conditional Where → Count → OrderBy → Skip/Take → map → PagedResult.
internal sealed class PutawayTaskReader(InventoryDbContext db) : IPutawayTaskReader
{
    public async Task<PagedResult<PutawayTaskListItem>> ListAsync(
        string? assignedTo = null,
        PutawayTaskStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        var query = db.PutawayTasks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(assignedTo))
            query = query.Where(task => task.AssignedTo == assignedTo);
        if (status is not null)
            query = query.Where(task => task.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var tasks = await query
            .OrderBy(task => task.Status)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var items = tasks
            .Select(task => new PutawayTaskListItem(
                task.Id.Value,
                task.StockId.Value,
                task.SourceLocationId,
                task.SuggestedDestinationId,
                task.ActualDestinationId,
                task.AssignedTo,
                task.Status.ToString()))
            .ToList();

        return new PagedResult<PutawayTaskListItem>(items, safePage, safeSize, totalCount);
    }
}
