using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.Features.HoldGoodsReceipt;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — SPV hold seluruh GR (Pending→Hold)
// How: POST /goods-receipts/{id}/hold → HoldGoodsReceiptCommand (auditable, TIDAK emit event).
public sealed class HoldGoodsReceiptEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.HoldGR
        app.MapPost("/goods-receipts/{id:guid}/hold", async (
            Guid id,
            HoldGoodsReceiptRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new HoldGoodsReceiptCommand(id, request.Reason), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}

public sealed record HoldGoodsReceiptRequest(string Reason);
