using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Reporting.Messaging;
using Wms.Reporting.Persistence;
using Wms.Reporting.Projectors;
using Wms.Reporting.Rebuild;
using Wms.Reporting.Stores;

namespace Wms.Reporting.DependencyInjection;

// What: composition modul Reporting (collapsed) — AddReporting pattern (blueprint §3)
// Why: host cukup AddReporting(connStr). DbContext + map DbContext→ReportingDbContext (rail generik bekerja
// atas DbContext ambient, FF#4/#6) + transactional messaging (UoW/Inbox) + per-type store + projector +
// dispatcher + rebuilder. PURE CONSUMER: TANPA AuditableEntityInterceptor (projection bukan IAuditable) &
// TANPA MediatR (projector = plain class via dispatcher; query = read-side bypass langsung DbContext).
// How: AddDbContext (snake_case + migrations-history schema "reporting"); AddScoped<DbContext> delegasi
// (UoW/InboxGuard/store berbagi instance DbContext yang SAMA → commit projection-write + Inbox atomic);
// store/projector scoped; dispatcher singleton (pegang IServiceScopeFactory); rebuilder scoped.
public static class ReportingExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ReportingDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", ReportingDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ReportingDbContext>());
        services.AddTransactionalMessaging();

        services.AddScoped<IStockOnHandViewStore, StockOnHandViewStore>();
        services.AddScoped<IReceivingSummaryStore, ReceivingSummaryStore>();
        services.AddScoped<IDispatchSummaryStore, DispatchSummaryStore>();
        services.AddScoped<IOperatorActivityStore, OperatorActivityStore>();

        services.AddScoped<GoodsReceiptConfirmedProjector>();
        services.AddScoped<StockRemovedProjector>();
        services.AddScoped<PutawayCompletedProjector>();
        services.AddScoped<PickingCompletedProjector>();

        services.AddSingleton<ReportingIntegrationEventDispatcher>();
        services.AddScoped<ProjectionRebuilder>();
        return services;
    }
}
