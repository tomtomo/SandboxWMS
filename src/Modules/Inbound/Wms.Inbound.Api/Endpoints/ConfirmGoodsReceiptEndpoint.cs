using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.Features.ConfirmGoodsReceipt;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — konfirmasi GoodsReceipt
// Why: memicu GRConfirmed→Outbox; Result→HTTP via ToProblemDetails — NotFound (404) vs
// Conflict (409, sudah Confirmed) jatuh OTOMATIS dari Error.Type (ADR-0019), tak perlu cabang manual.
// How: POST /goods-receipts/{id}/confirm → sender.Send → NoContent saat sukses, ToProblemDetails saat gagal.
public sealed class ConfirmGoodsReceiptEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.PostGR
        app.MapPost("/goods-receipts/{id:guid}/confirm", async (
            Guid id,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ConfirmGoodsReceiptCommand(id), cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : result.ToProblemDetails();
        });
    }
}
