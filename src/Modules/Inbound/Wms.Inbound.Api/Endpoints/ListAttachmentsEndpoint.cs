using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Inbound.Application.Abstractions;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — list attachment satu GR.
// Why: WebUI butuh daftar dokumen pendukung sebuah GR. Read-side BYPASS: delegasi ke IGRAttachmentReader
// (read-port), TANPA aggregate/repo/MediatR. Inject reader (bukan DbContext) → FF#8. Response BARE ARRAY
// (bukan PagedResult): himpunan attachment per GR berbatas-alami (bounded by aggregate scope).
// How: GET /goods-receipts/{id:guid}/attachments → reader.ListByGoodsReceiptAsync → Ok(array). AuthZ deferred (ADR-0012).
public sealed class ListAttachmentsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.ViewGR
        app.MapGet("/goods-receipts/{id:guid}/attachments", async (
            Guid id,
            IGRAttachmentReader reader,
            CancellationToken cancellationToken) =>
        {
            var attachments = await reader.ListByGoodsReceiptAsync(id, cancellationToken);
            return Results.Ok(attachments);
        });
    }
}
