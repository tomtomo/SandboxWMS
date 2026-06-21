using Microsoft.AspNetCore.Routing;
using Wms.Inbound.Api.Endpoints;

namespace Wms.Inbound.Api;

// What: composition endpoint modul Inbound (REST; ADR-0006)
// Why: host cukup app.MapInboundEndpoints() — semua slice REST terdaftar di satu tempat (bukan
// controller terpusat). Tambah slice = tambah satu IEndpoint + satu baris di sini. Urutan mengikuti
// flow GR: create → scan → declare → resolve → confirm/hold; plus upload attachment.
public static class InboundApiExtensions
{
    public static IEndpointRouteBuilder MapInboundEndpoints(this IEndpointRouteBuilder app)
    {
        new CreateGoodsReceiptEndpoint().MapEndpoint(app);
        new ScanItemEndpoint().MapEndpoint(app);
        new DeclareScanCompleteEndpoint().MapEndpoint(app);
        new ResolveDiscrepancyEndpoint().MapEndpoint(app);
        new ConfirmGoodsReceiptEndpoint().MapEndpoint(app);
        new HoldGoodsReceiptEndpoint().MapEndpoint(app);
        new UploadAttachmentEndpoint().MapEndpoint(app);
        return app;
    }
}
