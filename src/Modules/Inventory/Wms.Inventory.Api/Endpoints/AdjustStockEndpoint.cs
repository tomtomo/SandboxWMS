using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inventory.Application.Features.AdjustStock;

namespace Wms.Inventory.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — koreksi manual kuantitas Stock
// Why: operator mengoreksi balance fisik (cycle count). Result→HTTP via ToProblemDetails — NotFound
// (404, stock hilang) vs Validation (400, qty negatif) jatuh OTOMATIS dari Error.Type (ADR-0019).
// How: POST /stocks/{id}/adjust → AdjustStockCommand → NoContent / ToProblemDetails.
public sealed class AdjustStockEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inventory.AdjustStock
        app.MapPost("/stocks/{id:guid}/adjust", async (
            Guid id,
            AdjustStockRequest request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new AdjustStockCommand(id, request.NewQty), cancellationToken);

            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });
    }
}

public sealed record AdjustStockRequest(int NewQty);
