using Microsoft.Extensions.DependencyInjection;

namespace Wms.Notification.Directory;

// What: registrasi adapter IUserDirectory/IWarehouseDirectory → gRPC (ACL read-API; ADR-0011)
// Why: host me-register gRPC client + resilience pipeline; extension ini menyambungkan PORT ke ADAPTER
// internal (adapter tetap internal — host tak perlu lihat tipenya). DIPISAH dari AddNotification secara
// SENGAJA: integration test memakai stub IUserDirectory/IWarehouseDirectory — bila adapter di-register di
// AddNotification, ia MENIMPA stub (last-wins) → resolve adapter gRPC tanpa client → DI gagal. Host
// memanggil ini; test tidak (pola sama AddMasterDataProductCatalog di Inbound).
public static class DirectoryClientExtensions
{
    public static IServiceCollection AddNotificationDirectories(this IServiceCollection services)
    {
        services.AddScoped<IUserDirectory, GrpcUserDirectory>();
        services.AddScoped<IWarehouseDirectory, GrpcWarehouseDirectory>();
        return services;
    }
}
