using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks.Application.Pagination;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Application.ReadModels;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — realisasi IPickingTaskReader
// Why: endpoint REST (*.Api) tak menyentuh DbContext (FF#8). DEDIKASI read-side (bukan reuse
// IPickingTaskRepository tracked/unpaged write-side): AsNoTracking, paginated, conditional filter.
// Materialize-then-map (Status enum→string; strongly-typed id → .Value in-memory).
// How: conditional Where per filter non-null (assignedTo/status/waveId); status string di-parse ke enum
// (kolom disimpan enum-NAME). clamp → Count → OrderBy → Skip/Take → map → PagedResult.
internal sealed class PickingTaskReader(OutboundDbContext db) : IPickingTaskReader
{
    public async Task<PagedResult<PickingTaskReadModel>> ListAsync(
        string? assignedTo = null,
        string? status = null,
        Guid? waveId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (safePage, safeSize) = PageRequest.From(page, pageSize);

        var query = db.PickingTasks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(assignedTo))
            query = query.Where(task => task.AssignedTo == assignedTo);
        if (Enum.TryParse<PickingTaskStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(task => task.Status == parsedStatus);
        if (waveId is { } wave)
            query = query.Where(task => task.WaveId == wave);

        var totalCount = await query.CountAsync(cancellationToken);

        var tasks = await query
            .OrderByDescending(task => task.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var items = tasks
            .Select(task => new PickingTaskReadModel(
                task.Id.Value,
                task.WaveId,
                task.Sku,
                task.Batch,
                task.Qty,
                task.AssignedTo,
                task.Status.ToString(),
                task.StagingLocationId))
            .ToList();

        return new PagedResult<PickingTaskReadModel>(items, safePage, safeSize, totalCount);
    }
}
