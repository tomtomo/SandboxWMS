using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inventory.Application.Features.CompletePutaway;

namespace Wms.Inventory.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — selesaikan putaway
// Why: operator scan stock + scan destination rak → Stock OnHand→Available. Result→HTTP via
// ToProblemDetails — NotFound (404, task/stock hilang) vs Conflict (409, task bukan Assigned) jatuh
// OTOMATIS dari Error.Type (ADR-0019), tak perlu cabang manual.
// How: POST /putaway-tasks/{id}/complete → CompletePutawayCommand → NoContent / ToProblemDetails.
public sealed class CompletePutawayEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inventory.CompletePutaway
        app.MapPost("/putaway-tasks/{id:guid}/complete", async (
            Guid id,
            CompletePutawayRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new CompletePutawayCommand(id, request.ActualDestinationId), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}

public sealed record CompletePutawayRequest(string ActualDestinationId);
