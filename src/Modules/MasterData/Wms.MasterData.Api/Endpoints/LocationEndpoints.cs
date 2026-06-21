using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Application.Features.CreateLocation;
using Wms.MasterData.Application.Features.DeactivateLocation;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Api.Endpoints;

// What: REST endpoints resource Location (CRUD manajemen; ADR-0006)
// Why: Type diterima sebagai STRING lalu di-parse ke LocationType (Enum.TryParse) — tak bergantung
// konfigurasi JsonStringEnumConverter di host; input invalid → 400 (Validation).
public sealed class LocationEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/locations");

        // TODO-AUTH: MasterData.ManageLocation
        group.MapPost("/", async (CreateLocationRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<LocationType>(request.Type, ignoreCase: true, out var type))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Failed", detail: $"type '{request.Type}' tidak dikenal.");

            var result = await sender.Send(
                new CreateLocationCommand(request.WarehouseId, type, request.Code), cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/locations/{result.Value}", new { locationId = result.Value })
                : result.ToProblemDetails();
        });

        // TODO-AUTH: MasterData.ManageLocation
        group.MapPost("/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new DeactivateLocationCommand(id), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.ToProblemDetails();
        });

        group.MapGet("/{id:guid}", async (Guid id, IMasterDataReader reader, CancellationToken cancellationToken) =>
        {
            var location = await reader.GetLocationAsync(id, cancellationToken);
            return location is null ? Results.NotFound() : Results.Ok(location);
        });
    }
}

public sealed record CreateLocationRequest(Guid WarehouseId, string Type, string Code);
