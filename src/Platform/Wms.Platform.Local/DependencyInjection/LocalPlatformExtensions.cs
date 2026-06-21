using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Messaging;
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
}
