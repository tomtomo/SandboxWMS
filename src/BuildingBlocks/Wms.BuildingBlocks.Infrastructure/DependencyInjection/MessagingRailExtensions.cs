using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Messaging;

namespace Wms.BuildingBlocks.Infrastructure.DependencyInjection;

// What: composition helper rail messaging (Outbox relay)
// Why: host cukup AddOutboxDispatcher() — registrasi BackgroundService + opsi
// terkunci di satu tempat agnostic, konsisten lintas service. Adapter konkret
// (IMessagePublisher/IDeadLetterStore) di-wire terpisah oleh Platform.<Cloud>.
// How: OutboxOptions sebagai singleton + OutboxDispatcher sebagai hosted service.
public static class MessagingRailExtensions
{
    public static IServiceCollection AddOutboxDispatcher(
        this IServiceCollection services, Action<OutboxOptions>? configure = null)
    {
        var options = new OutboxOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddHostedService<OutboxDispatcher>();
        return services;
    }
}
