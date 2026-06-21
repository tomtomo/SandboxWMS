using Microsoft.AspNetCore.Routing;
using Wms.MasterData.Api.Endpoints;

namespace Wms.MasterData.Api;

// What: composition endpoint REST modul MasterData (ADR-0006)
// Why: host cukup app.MapMasterDataEndpoints() — semua resource (Product/Warehouse/Location) terdaftar
// di satu tempat. gRPC read-API service (MasterDataReadService) di-map terpisah oleh host
// (app.MapGrpcService<MasterDataReadService>()) karena butuh runtime Grpc.AspNetCore.
public static class MasterDataApiExtensions
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        new ProductEndpoints().MapEndpoint(app);
        new WarehouseEndpoints().MapEndpoint(app);
        new LocationEndpoints().MapEndpoint(app);
        return app;
    }
}
