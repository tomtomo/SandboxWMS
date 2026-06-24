using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wms.BuildingBlocks.Web;
using Wms.Outbound.Application.Abstractions;

namespace Wms.Outbound.Api.Endpoints;

// What: Minimal API endpoint (Vertical Slice + CQRS read-side; ADR-0004/0006) — detail Wave
// Why: WebUI halaman detail wave (order tergabung + union OrderLines). Read-side BYPASS: delegasi ke
// IWaveReader, inject reader (bukan DbContext) → FF#8. NotFound saat wave tak ada.
// How: GET /waves/{id:guid} → reader.GetAsync → 404 bila null, else 200 Ok(WaveDetail).
// AuthZ deferred (ADR-0012) → TODO-AUTH.
public sealed class GetWaveEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // TODO-AUTH: Outbound.ViewWaves
        app.MapGet("/waves/{id:guid}", async (
            Guid id,
            IWaveReader reader,
            CancellationToken cancellationToken) =>
        {
            var wave = await reader.GetAsync(id, cancellationToken);
            return wave is null ? Results.NotFound() : Results.Ok(wave);
        });
    }
}
