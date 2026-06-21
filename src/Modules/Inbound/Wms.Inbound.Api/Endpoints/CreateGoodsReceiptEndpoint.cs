using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.Features.CreateGoodsReceipt;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + REST untuk UI; ADR-0006)
// Why: slice mendaftarkan route-nya sendiri (IEndpoint); request masuk pipeline lewat ISender,
// Result.Failure dipetakan ke RFC 7807 ProblemDetails (ADR-0019) — bukan bentuk error ad-hoc.
// How: POST /goods-receipts → sender.Send(command) → Created saat sukses, ToProblemDetails saat gagal.
public sealed class CreateGoodsReceiptEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.CreateGR
        app.MapPost("/goods-receipts", async (
            CreateGoodsReceiptCommand command,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/goods-receipts/{result.Value}", new { id = result.Value })
                : result.ToProblemDetails();
        });
    }
}
