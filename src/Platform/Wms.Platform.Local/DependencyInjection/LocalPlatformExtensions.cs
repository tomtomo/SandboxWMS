using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.Platform.Local.Auditing;
using Wms.Platform.Local.Messaging;

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
}
