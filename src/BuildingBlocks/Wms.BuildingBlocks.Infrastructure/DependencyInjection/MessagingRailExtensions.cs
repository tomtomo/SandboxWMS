using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Abstractions;
using Wms.BuildingBlocks.Application.Messaging;
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

    // What: registrasi consumer-side retry → Dead Letter Channel pipeline (ADR-0005/0010)
    // Why: host consumer cukup AddConsumerDeadLettering() lalu bungkus subscriber-nya dengan
    // pipeline.Wrap(source, handler). Opsi + pipeline singleton (stateless, resolve scope
    // sendiri per dead-letter). IDeadLetterStore tetap di-wire oleh Platform.<Cloud>.
    public static IServiceCollection AddConsumerDeadLettering(
        this IServiceCollection services, Action<ConsumerRetryOptions>? configure = null)
    {
        var options = new ConsumerRetryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<ConsumerDeadLetterPipeline>();
        return services;
    }

    // What: registrasi transactional messaging primitives (UoW + outbox-writer + inbox-guard)
    // Why: producer butuh IUnitOfWork + IIntegrationEventOutbox; consumer butuh IUnitOfWork
    // + IInboxGuard. Ketiganya scoped atas DbContext ambient modul (DB-per-service). Host
    // panggil sekali; impl internal — BuildingBlocks tak bocorkan tipe konkret ke konsumen.
    // Registrasi yang tak terpakai (mis. outbox-writer di host consumer-only) tak berbiaya
    // sampai di-resolve.
    public static IServiceCollection AddTransactionalMessaging(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IIntegrationEventOutbox, OutboxIntegrationEventWriter>();
        services.AddScoped<IInboxGuard, InboxGuard>();
        return services;
    }
}
