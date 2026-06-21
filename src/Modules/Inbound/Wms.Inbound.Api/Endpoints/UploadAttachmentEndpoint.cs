using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Inbound.Application.Features.UploadAttachment;

namespace Wms.Inbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — upload dokumen pendukung GR (multipart)
// How: POST /goods-receipts/{id}/attachments (form-data file) → UploadAttachmentCommand → byte ke
// object storage + row metadata. DisableAntiforgery: API stateless (token antiforgery tak relevan;
// authZ via JWT kelak — TODO-AUTH).
public sealed class UploadAttachmentEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Inbound.UploadAttachment
        app.MapPost("/goods-receipts/{id:guid}/attachments", async (
            Guid id,
            IFormFile file,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadAttachmentCommand(
                id, file.FileName, file.ContentType, file.Length, stream), cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/goods-receipts/{id}/attachments/{result.Value}", new { id = result.Value })
                : result.ToProblemDetails();
        }).DisableAntiforgery();
    }
}
