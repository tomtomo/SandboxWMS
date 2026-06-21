using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Outbound.Application.Features.CompletePicking;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — operator selesaikan picking (overview §C5)
// Why: PickingTask Assigned→Completed → emit PickingCompleted (Stock Allocated→Picked di Inventory) + gate
// Wave→Ready. Result→HTTP via ToProblemDetails — NotFound (404, task hilang) vs Conflict (409, bukan Assigned)
// jatuh OTOMATIS dari Error.Type (ADR-0019).
// How: POST /picking-tasks/{id}/complete → CompletePickingCommand → NoContent saat sukses, ToProblemDetails saat gagal.
public sealed class CompletePickingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Outbound.CompletePicking
        app.MapPost("/picking-tasks/{id:guid}/complete", async (
            Guid id,
            CompletePickingRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new CompletePickingCommand(id, request.StagingLocationId), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}

public sealed record CompletePickingRequest(string StagingLocationId);
