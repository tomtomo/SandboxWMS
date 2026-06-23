using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Application.Features.CreateProduct;
using Wms.MasterData.Application.Features.DeactivateProduct;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Api.Endpoints;

// What: REST endpoints resource Product (CRUD manajemen; ADR-0006 REST untuk UI/gateway)
// Why: di-group per-resource (REST convention) — Create/Deactivate (write, via MediatR slice) + GET
// (read, via IMasterDataReader cache-aside). gRPC tetap jalur read service-to-service; REST untuk
// manajemen/UI. Result→HTTP via ToProblemDetails (status dari Error.Type otomatis, ADR-0019).
public sealed class ProductEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        // TODO-AUTH: MasterData.ManageProduct
        group.MapPost("/", async (CreateProductRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new CreateProductCommand(
                    request.Sku, request.Name, request.Uom, request.BatchTrackingRequired,
                    request.ExpiryTrackingRequired, request.QcRequiredOnReceipt, request.ShelfLifeDays),
                cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/products/{result.Value}", new { id = result.Value })
                : result.ToProblemDetails();
        });

        // TODO-AUTH: MasterData.ManageProduct
        group.MapPost("/{sku}/deactivate", async (string sku, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeactivateProductCommand(sku), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });

        group.MapGet("/{sku}", async (string sku, IMasterDataReader reader, CancellationToken cancellationToken) =>
        {
            var product = await reader.GetProductAsync(sku, cancellationToken);
            return product is null ? ProductErrors.NotFound.ToProblemDetails() : Results.Ok(product);
        });
    }
}

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string Uom,
    bool BatchTrackingRequired,
    bool ExpiryTrackingRequired,
    bool QcRequiredOnReceipt,
    int? ShelfLifeDays);
