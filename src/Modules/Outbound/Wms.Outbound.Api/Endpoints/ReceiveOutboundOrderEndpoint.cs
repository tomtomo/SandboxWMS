using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Outbound.Application.Features.ReceiveOutboundOrder;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + REST; ADR-0006) — terima order eksternal (overview §C1)
// Why: order masuk WMS → OutboundOrder New (id baru di-return). Bukan tindakan operator (datang dari sistem
// eksternal, tak ada di permission catalog §E) → TANPA marker // TODO-AUTH. Result→HTTP via ToProblemDetails.
// How: POST /outbound-orders → command (di-bind dari body) → Created saat sukses, ToProblemDetails saat gagal.
public sealed class ReceiveOutboundOrderEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/outbound-orders", async (
            ReceiveOutboundOrderCommand command,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/outbound-orders/{result.Value}", new { id = result.Value })
                : result.ToProblemDetails();
        });
    }
}
