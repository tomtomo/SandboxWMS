using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.Features.ScanItem;
using Wms.Inbound.Domain;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — operator scan satu line
// How: POST /goods-receipts/{id}/scans → ScanItemCommand → NoContent / ToProblemDetails.
public sealed class ScanItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.ScanItem
        app.MapPost("/goods-receipts/{id:guid}/scans", async (
            Guid id,
            ScanItemRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ScanItemCommand(
                id, request.Sku, request.ActualQty, request.Batch, request.Expiry, request.LineStatus),
                cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}

public sealed record ScanItemRequest(
    string Sku, int ActualQty, string? Batch, DateOnly? Expiry, LineStatus LineStatus);
