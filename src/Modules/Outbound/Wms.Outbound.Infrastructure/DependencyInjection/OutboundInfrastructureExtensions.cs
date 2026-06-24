using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Auditing;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Application.Features.ConsumeStockAllocated;
using Wms.Outbound.Infrastructure.Messaging;
using Wms.Outbound.Infrastructure.Persistence;

namespace Wms.Outbound.Infrastructure.DependencyInjection;

// What: composition modul Outbound (Infrastructure) — AddXxxModule pattern (blueprint §3)
// Why: host cukup AddOutboundInfrastructure(connStr). Mendaftarkan DbContext modul + memetakan DbContext→
// OutboundDbContext (rail generik bekerja atas DbContext ambient, FF#4/#6) + transactional messaging (UoW/
// outbox-writer/inbox) + repos + consumer + dispatcher. Outbound = HYBRID: emit 3 event (Outbox) + consume
// StockAllocated (Inbox).
// How: AddDbContext + AddScoped<DbContext> delegasi; AddTransactionalMessaging; consumer scoped; dispatcher
// singleton (pegang IServiceScopeFactory). Interceptor audit di-resolve dari sp → ICurrentUser scoped benar.
public static class OutboundInfrastructureExtensions
{
    public static IServiceCollection AddOutboundInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddAuditableEntityInterceptor();
        services.AddDbContext<OutboundDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", OutboundDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<OutboundDbContext>());

        services.AddTransactionalMessaging();
        services.AddScoped<IOutboundOrderRepository, OutboundOrderRepository>();
        services.AddScoped<IWaveRepository, WaveRepository>();
        services.AddScoped<IPickingTaskRepository, PickingTaskRepository>();

        // read-port (CQRS read-side, ADR-0004): list/detail untuk WebUI tanpa lewat aggregate/repo
        services.AddScoped<IOutboundOrderReader, OutboundOrderReader>();
        services.AddScoped<IWaveReader, WaveReader>();
        services.AddScoped<IPickingTaskReader, PickingTaskReader>();

        // consumer integration-event (scoped per pesan; bukan MediatR handler) — Phase 03c: StockAllocated.
        services.AddScoped<StockAllocatedConsumer>();

        services.AddSingleton<OutboundIntegrationEventDispatcher>();
        return services;
    }
}
