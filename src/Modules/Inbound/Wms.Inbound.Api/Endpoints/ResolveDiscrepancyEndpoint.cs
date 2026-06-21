using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.Features.ResolveDiscrepancy;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — SPV resolve satu discrepancy
// How: POST /goods-receipts/{id}/discrepancies/resolve → ResolveDiscrepancyCommand (auditable).
public sealed class ResolveDiscrepancyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.ResolveDiscrepancy
        app.MapPost("/goods-receipts/{id:guid}/discrepancies/resolve", async (
            Guid id,
            ResolveDiscrepancyRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ResolveDiscrepancyCommand(
                id, request.Sku, request.Type, request.Action, request.Note), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}

public sealed record ResolveDiscrepancyRequest(
    string Sku, DiscrepancyType Type, ResolutionAction Action, string? Note);
