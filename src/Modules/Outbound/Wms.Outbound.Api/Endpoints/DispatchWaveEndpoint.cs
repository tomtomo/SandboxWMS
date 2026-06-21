using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Outbound.Application.Features.DispatchWave;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — SPV dispatch wave (overview §C6)
// Why: Wave Ready→Dispatched → emit ShipmentDispatched (Inventory remove Stock Picked) + order→Closed.
// Result→HTTP via ToProblemDetails — NotFound (404, wave hilang) vs Conflict (409, wave bukan Ready) OTOMATIS.
// How: POST /waves/{id}/dispatch → DispatchWaveCommand → NoContent saat sukses, ToProblemDetails saat gagal.
public sealed class DispatchWaveEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Outbound.DispatchWave
        app.MapPost("/waves/{id:guid}/dispatch", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DispatchWaveCommand(id), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}
