using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Outbound.Application.Abstractions;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — list Wave
// Why: WebUI butuh daftar wave. Read-side BYPASS: endpoint delegasi ke IWaveReader (read-port),
// TANPA aggregate/repo/MediatR. Inject reader (bukan DbContext) → FF#8.
// How: GET /waves?status=&page=&pageSize= → reader.ListAsync → Ok(PagedResult). Paginated.
// AuthZ deferred (ADR-0012) → TODO-AUTH.
public sealed class ListWavesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Outbound.ViewWaves
        app.MapGet("/waves", async (
            string? status,
            int? page,
            int? pageSize,
            IWaveReader reader,
            CancellationToken cancellationToken) =>
        {
            var result = await reader.ListAsync(status, page ?? 1, pageSize ?? 20, cancellationToken);
            return Results.Ok(result);
        });
    }
}
