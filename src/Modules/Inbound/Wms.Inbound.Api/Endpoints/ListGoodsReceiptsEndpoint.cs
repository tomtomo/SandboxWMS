using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Inbound.Application.Abstractions;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — list GoodsReceipt
// Why: WebUI (Phase 04e) butuh daftar GR. Read-side BYPASS: endpoint delegasi ke IGoodsReceiptReader
// (read-port), TANPA aggregate/repo/MediatR (bukan write path). Inject reader (bukan DbContext) → FF#8.
// How: GET /goods-receipts?warehouseId=&page=&pageSize= → reader.ListAsync → Ok(PagedResult). Paginated
// (cegah unbounded result set). Method GET berbeda dari POST create di path sama (no route conflict).
// AuthZ deferred (ADR-0012) → TODO-AUTH; enforcement 07a.
public sealed class ListGoodsReceiptsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.ViewGR
        app.MapGet("/goods-receipts", async (
            string? warehouseId,
            int? page,
            int? pageSize,
            IGoodsReceiptReader reader,
            CancellationToken cancellationToken) =>
        {
            var result = await reader.ListAsync(warehouseId, page ?? 1, pageSize ?? 20, cancellationToken);
            return Results.Ok(result);
        });
    }
}
