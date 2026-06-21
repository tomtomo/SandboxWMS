using Microsoft.Extensions.DependencyInjection;
using Wms.Outbound.Application.Abstractions;

namespace Wms.Outbound.Infrastructure.DependencyInjection;

// What: registrasi adapter IProductCatalog → GrpcProductCatalog (ACL gRPC read-API; ADR-0011)
// Why: host me-register gRPC client + resilience pipeline; extension ini menyambungkan PORT ke ADAPTER
// internal (adapter tetap internal — di-register di DALAM assembly-nya). DIPISAH dari
// AddOutboundInfrastructure secara SENGAJA: integration test memakai stub IProductCatalog — bila adapter
// di-register di AddOutboundInfrastructure, ia MENIMPA stub (last-wins) → DI gagal. Host panggil ini; test tidak.
public static class MasterDataClientExtensions
{
    public static IServiceCollection AddMasterDataProductCatalog(this IServiceCollection services)
    {
        services.AddScoped<IProductCatalog, MasterData.GrpcProductCatalog>();
        return services;
    }
}
