using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Storage;
using Wms.Platform.Local.Auditing;
using Wms.Platform.Local.Messaging;
using Wms.Platform.Local.Storage;

namespace Wms.Platform.Local.DependencyInjection;

// What: composition adapter Local (port → implementasi in-proc/Postgres)
// Why: host lokal cukup AddLocalMessaging() untuk memasang publisher in-proc + DLQ
// Postgres. Pemilihan adapter = keputusan deploy-time; di sini env Local.
// How: InMemoryMessagePublisher singleton, juga di-expose sebagai dirinya sendiri agar
// konsumer in-proc bisa Subscribe(); LocalDeadLetterStore scoped (butuh DbContext scoped).
public static class LocalPlatformExtensions
{
    public static IServiceCollection AddLocalMessaging(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryMessagePublisher>();
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<InMemoryMessagePublisher>());
        services.AddScoped<IDeadLetterStore, LocalDeadLetterStore>();
        return services;
    }

    // What: composition adapter audit Local (port IAuditLogStore → tabel Postgres audit_log)
    // Why: dipisah dari messaging — audit operasional (ADR-0022) concern berbeda dari rail.
    // Scoped: butuh DbContext scoped; AuditLogBehavior me-resolve-nya di SCOPE BARU per write.
    public static IServiceCollection AddLocalAuditing(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogStore, LocalAuditLogStore>();
        return services;
    }

    // What: composition adapter object-storage Local (port IObjectStore → filesystem)
    // Why: byte attachment (ADR-0015) disimpan di filesystem lokal di bawah satu root. Adapter cloud
    // (Blob/GCS) menggantikannya tanpa sentuh core. Singleton: stateless, root path tetap.
    public static IServiceCollection AddLocalObjectStore(this IServiceCollection services, string rootPath)
    {
        services.AddSingleton<IObjectStore>(new LocalObjectStore(rootPath));
        return services;
    }
}
