using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + REST untuk UI; ADR-0006)
// Why: slice mendaftarkan route-nya sendiri (IEndpoint) — host cukup MapInboundEndpoints,
// bukan controller terpusat. Result→HTTP minimal di sini; ProblemDetails penuh di
// Phase 02a (ADR-0019).
// How: POST /goods-receipts bind command dari body → invoke CreateGoodsReceiptHandler.
public sealed class CreateGoodsReceiptEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.CreateGR
        app.MapPost("/goods-receipts", async (
            CreateGoodsReceiptCommand command,
            CreateGoodsReceiptHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/goods-receipts/{result.Value}", new { id = result.Value })
                : Results.BadRequest(new { error = result.Error.Code, message = result.Error.Message });
        });
    }
}
