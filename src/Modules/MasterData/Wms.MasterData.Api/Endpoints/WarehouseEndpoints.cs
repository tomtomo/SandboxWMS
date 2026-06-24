using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Application.Features.CreateWarehouse;
using Wms.MasterData.Application.Features.DeactivateWarehouse;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Api.Endpoints;

// What: REST endpoints resource Warehouse (CRUD manajemen; ADR-0006)
public sealed class WarehouseEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/warehouses");

        // TODO-AUTH: MasterData.ManageWarehouse
        group.MapPost("/", async (CreateWarehouseRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new CreateWarehouseCommand(request.Name, request.Address), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/warehouses/{result.Value}", new { id = result.Value })
                : result.ToProblemDetails();
        });

        // TODO-AUTH: MasterData.ManageWarehouse
        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeactivateWarehouseCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });

        // TODO-AUTH: MasterData.ViewWarehouse
        group.MapGet("/", async (
            bool? isActive,
            int? page,
            int? pageSize,
            IMasterDataReader reader,
            CancellationToken cancellationToken) =>
        {
            var result = await reader.ListWarehousesAsync(page ?? 1, pageSize ?? 20, isActive, cancellationToken);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMasterDataReader reader, CancellationToken cancellationToken) =>
        {
            var warehouse = await reader.GetWarehouseAsync(id, cancellationToken);
            return warehouse is null ? WarehouseErrors.NotFound.ToProblemDetails() : Results.Ok(warehouse);
        });
    }
}

public sealed record CreateWarehouseRequest(string Name, string Address);
