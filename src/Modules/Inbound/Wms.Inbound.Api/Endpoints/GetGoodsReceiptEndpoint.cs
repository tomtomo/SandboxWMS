using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Inbound.Application.Abstractions;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — detail satu GoodsReceipt.
// Why: WebUI butuh halaman detail GR (header + expected/scanned/discrepancy). Read-side BYPASS: endpoint
// delegasi ke IGoodsReceiptReader (read-port), TANPA aggregate/repo/MediatR. Inject reader (bukan DbContext) → FF#8.
// How: GET /goods-receipts/{id:guid} → reader.GetByIdAsync → null ? 404 : Ok(detail). AuthZ deferred (ADR-0012).
public sealed class GetGoodsReceiptEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.ViewGR
        app.MapGet("/goods-receipts/{id:guid}", async (
            Guid id,
            IGoodsReceiptReader reader,
            CancellationToken cancellationToken) =>
        {
            var detail = await reader.GetByIdAsync(id, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });
    }
}
