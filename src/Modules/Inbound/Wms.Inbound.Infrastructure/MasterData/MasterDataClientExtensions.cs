using Microsoft.Extensions.DependencyInjection;
using Wms.Inbound.Application.Abstractions;

namespace Wms.Inbound.Infrastructure.DependencyInjection;

// What: registrasi adapter IProductCatalog → GrpcProductCatalog (ACL gRPC read-API; ADR-0011)
// Why: host me-register gRPC client + resilience pipeline; extension ini menyambungkan PORT ke ADAPTER
// internal (adapter tetap internal — di-register di DALAM assembly-nya, host tak perlu lihat tipenya).
// DIPISAH dari AddInboundInfrastructure secara SENGAJA: MigrationRunner & integration test memakai stub
// IProductCatalog — bila adapter di-register di AddInboundInfrastructure, ia akan MENIMPA stub (last-wins)
// → handler resolve GrpcProductCatalog tanpa gRPC client → DI gagal. Host memanggil ini; test tidak.
public static class MasterDataClientExtensions
{
    public static IServiceCollection AddMasterDataProductCatalog(this IServiceCollection services)
    {
        services.AddScoped<IProductCatalog, MasterData.GrpcProductCatalog>();
        return services;
    }
}
