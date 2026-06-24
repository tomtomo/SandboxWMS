using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — list PutawayTask
// Why: WebUI butuh daftar instruksi putaway (work queue operator). Read-side BYPASS: endpoint delegasi
// ke IPutawayTaskReader (read-port), TANPA aggregate/repo/MediatR. Inject reader (bukan DbContext) → FF#8.
// Method GET pada /putaway-tasks TAK bentrok dengan POST /putaway-tasks/{id}/complete (verb + path beda).
// How: GET /putaway-tasks?assignedTo=&status=&page=&pageSize= → reader.ListAsync → Ok(PagedResult).
// AuthZ deferred (ADR-0012) → TODO-AUTH; enforcement 07a.
public sealed class ListPutawayTasksEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inventory.ViewPutaway
        app.MapGet("/putaway-tasks", async (
            string? assignedTo,
            PutawayTaskStatus? status,
            int? page,
            int? pageSize,
            IPutawayTaskReader reader,
            CancellationToken cancellationToken) =>
        {
            var result = await reader.ListAsync(
                assignedTo, status, page ?? 1, pageSize ?? 20, cancellationToken);
            return Results.Ok(result);
        });
    }
}
