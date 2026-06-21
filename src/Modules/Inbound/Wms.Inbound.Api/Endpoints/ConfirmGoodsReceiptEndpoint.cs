using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Domain.Results;
using Wms.BuildingBlocks.Web;
using Wms.Inbound.Application.Features.ConfirmGoodsReceipt;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — konfirmasi GoodsReceipt
// Why: memicu GRConfirmed→Outbox; Result→HTTP membedakan NotFound (404) vs Conflict
// (409, sudah Confirmed) — pemetaan dari ErrorType (ADR-0019).
public sealed class ConfirmGoodsReceiptEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.PostGR
        app.MapPost("/goods-receipts/{id:guid}/confirm", async (
            Guid id,
            ConfirmGoodsReceiptHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(new ConfirmGoodsReceiptCommand(id), cancellationToken);
            if (result.IsSuccess)
                return Results.NoContent();

            var body = new { error = result.Error.Code, message = result.Error.Message };
            return result.Error.Type == ErrorType.NotFound
                ? Results.NotFound(body)
                : Results.Conflict(body);
        });
    }
}
