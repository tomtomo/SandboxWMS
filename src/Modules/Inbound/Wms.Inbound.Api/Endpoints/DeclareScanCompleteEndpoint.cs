using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.Features.DeclareScanComplete;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — declare scan selesai (InProgress→Pending)
// How: POST /goods-receipts/{id}/scan-complete → memicu kompilasi discrepancy dua-sumbu.
public sealed class DeclareScanCompleteEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.ScanItem
        app.MapPost("/goods-receipts/{id:guid}/scan-complete", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeclareScanCompleteCommand(id), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}
