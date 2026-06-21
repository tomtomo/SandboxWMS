using Microsoft.AspNetCore.Routing;
using Wms.Inventory.Api.Endpoints;

namespace Wms.Inventory.Api;

// What: composition endpoint modul Inventory (REST; ADR-0006)
// Why: host cukup app.MapInventoryEndpoints() — semua slice REST terdaftar di satu tempat (bukan
// controller terpusat). Tambah slice = tambah satu IEndpoint + satu baris di sini. Inventory adalah
// modul consumer-heavy (event-driven); REST hanya untuk operasi operator (CompletePutaway).
public static class InventoryApiExtensions
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        new CompletePutawayEndpoint().MapEndpoint(app);
        return app;
    }
}
