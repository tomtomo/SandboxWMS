using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Outbound.Application.Abstractions;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — detail OutboundOrder
// Why: WebUI halaman detail order (memanggil endpoint ini). Read-side BYPASS: delegasi ke
// IOutboundOrderReader, inject reader (bukan DbContext) → FF#8. NotFound saat order tak ada.
// How: GET /outbound-orders/{id:guid} → reader.GetAsync → 404 bila null, else 200 Ok(OrderDetail).
// AuthZ deferred (ADR-0012) → TODO-AUTH.
public sealed class GetOutboundOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Outbound.ViewOrders
        app.MapGet("/outbound-orders/{id:guid}", async (
            Guid id,
            IOutboundOrderReader reader,
            CancellationToken cancellationToken) =>
        {
            var order = await reader.GetAsync(id, cancellationToken);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });
    }
}
