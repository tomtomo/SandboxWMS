using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Outbound.Application.Abstractions;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — list PickingTask (P0)
// Why: papan kerja operator butuh daftar task ter-filter (assignedTo/status/waveId). Read-side BYPASS:
// delegasi ke IPickingTaskReader (DEDIKASI, bukan repository write-side), inject reader (bukan DbContext) → FF#8.
// How: GET /picking-tasks?assignedTo=&status=&waveId=&page=&pageSize= → reader.ListAsync → Ok(PagedResult).
// AuthZ deferred (ADR-0012) → TODO-AUTH.
public sealed class ListPickingTasksEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Outbound.ViewPickingTasks
        app.MapGet("/picking-tasks", async (
            string? assignedTo,
            string? status,
            Guid? waveId,
            int? page,
            int? pageSize,
            IPickingTaskReader reader,
            CancellationToken cancellationToken) =>
        {
            var result = await reader.ListAsync(assignedTo, status, waveId, page ?? 1, pageSize ?? 20, cancellationToken);
            return Results.Ok(result);
        });
    }
}
