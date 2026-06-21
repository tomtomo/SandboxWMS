using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.BuildingBlocks.Web.ErrorHandling;
using Wms.Outbound.Application.Features.CreateWave;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice; ADR-0006) — SPV buat wave (overview §C2)
// Why: memicu order→InProgress + Wave Active + WaveReleased→Outbox; id wave baru di-return. Result→HTTP via
// ToProblemDetails — NotFound (404, order hilang) vs Conflict (409, order bukan New) jatuh OTOMATIS dari Error.Type.
// How: POST /waves → command (body) → Created saat sukses, ToProblemDetails saat gagal.
public sealed class CreateWaveEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Outbound.CreateWave
        app.MapPost("/waves", async (
            CreateWaveCommand command,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/waves/{result.Value}", new { id = result.Value })
                : result.ToProblemDetails();
        });
    }
}
