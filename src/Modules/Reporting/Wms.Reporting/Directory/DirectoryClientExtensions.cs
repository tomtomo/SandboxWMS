using Microsoft.Extensions.DependencyInjection;

namespace Wms.Reporting.Directory;

// What: registrasi adapter IUserDirectory → gRPC (ACL read-API; ADR-0011)
// Why: host me-register gRPC client + resilience pipeline; extension ini menyambung PORT ke ADAPTER internal
// (adapter tetap internal — host tak perlu lihat tipenya). DIPISAH dari AddReporting secara SENGAJA: integration
// test memakai stub IUserDirectory; bila adapter di-register di AddReporting ia MENIMPA stub (last-wins) →
// resolve adapter gRPC tanpa server → gagal. Host memanggil ini; test meng-override dgn stub (pola sama
// AddNotificationDirectories di Notification).
public static class DirectoryClientExtensions
{
    public static IServiceCollection AddReportingDirectories(this IServiceCollection services)
    {
        services.AddScoped<IUserDirectory, GrpcUserDirectory>();
        return services;
    }
}
