using Microsoft.Extensions.DependencyInjection;
using Wms.Inventory.Application.Abstractions;

namespace Wms.Inventory.Infrastructure.DependencyInjection;

// What: registrasi adapter ILocationCatalog → GrpcLocationCatalog (ACL gRPC read-API; ADR-0011)
// Why: host me-register gRPC client + resilience pipeline; extension ini menyambung PORT ke ADAPTER
// internal (tetap internal — di-register di DALAM assembly-nya). DIPISAH dari AddInventoryInfrastructure
// secara SENGAJA: integration test memakai stub ILocationCatalog — bila adapter di-register di
// AddInventoryInfrastructure, ia MENIMPA stub (last-wins) → GoodsReceiptConfirmedConsumer resolve adapter
// gRPC tanpa client → DI gagal. Host panggil ini; test tidak.
public static class MasterDataLocationCatalogExtensions
{
    public static IServiceCollection AddMasterDataLocationCatalog(this IServiceCollection services)
    {
        services.AddScoped<ILocationCatalog, MasterData.GrpcLocationCatalog>();
        return services;
    }
}
