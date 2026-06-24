using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Domain;

namespace Wms.Inventory.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — list Stock
// Why: WebUI (Phase inventory) butuh daftar balance stock. Read-side BYPASS: endpoint delegasi ke
// IStockReader (read-port), TANPA aggregate/repo/MediatR (bukan write path). Inject reader (bukan
// DbContext) → FF#8. status di-bind sebagai StockStatus? dari query (string-name via host converter).
// How: GET /stocks?warehouseId=&sku=&status=&page=&pageSize= → reader.ListAsync → Ok(PagedResult).
// Paginated (cegah unbounded result set). AuthZ deferred (ADR-0012) → TODO-AUTH; enforcement 07a.
public sealed class ListStocksEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inventory.ViewStock
        app.MapGet("/stocks", async (
            string? warehouseId,
            string? sku,
            StockStatus? status,
            int? page,
            int? pageSize,
            IStockReader reader,
            CancellationToken cancellationToken) =>
        {
            var result = await reader.ListAsync(
                warehouseId, sku, status, page ?? 1, pageSize ?? 20, cancellationToken);
            return Results.Ok(result);
        });
    }
}
