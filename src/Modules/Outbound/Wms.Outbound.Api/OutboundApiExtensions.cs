using Microsoft.AspNetCore.Routing;
using Wms.Outbound.Api.Endpoints;

namespace Wms.Outbound.Api;

// What: composition endpoint modul Outbound (REST; ADR-0006)
// Why: host cukup app.MapOutboundEndpoints() — semua slice REST terdaftar di satu tempat (bukan controller
// terpusat). Tambah slice = tambah satu IEndpoint + satu baris di sini. Outbound = modul HYBRID: REST untuk
// operasi SPV/operator (ReceiveOrder/CreateWave/CompletePicking/DispatchWave) + consumer StockAllocated.
public static class OutboundApiExtensions
{
    public static IEndpointRouteBuilder MapOutboundEndpoints(this IEndpointRouteBuilder app)
    {
        new ReceiveOutboundOrderEndpoint().MapEndpoint(app);
        new CreateWaveEndpoint().MapEndpoint(app);
        new CompletePickingEndpoint().MapEndpoint(app);
        new DispatchWaveEndpoint().MapEndpoint(app);
        return app;
    }
}
