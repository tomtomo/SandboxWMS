using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.Auditing;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Inventory.Application.Abstractions;
using Wms.Inventory.Application.Features.ConsumeGoodsReceiptConfirmed;
using Wms.Inventory.Infrastructure.Messaging;
using Wms.Inventory.Infrastructure.Persistence;

namespace Wms.Inventory.Infrastructure.DependencyInjection;

// What: composition modul Inventory (Infrastructure) — AddXxxModule pattern (blueprint §3)
// Why: host cukup AddInventoryInfrastructure(connStr). Mendaftarkan DbContext modul +
// memetakan DbContext→InventoryDbContext (rail generik bekerja atas DbContext ambient,
// FF#4/#6) + transactional messaging + repos + consumer + dispatcher.
// How: AddDbContext + AddScoped<DbContext> delegasi; AddTransactionalMessaging (UoW/inbox);
// consumer scoped; dispatcher singleton (pegang IServiceScopeFactory). Interceptor audit
// di-resolve dari sp (overload (sp,options)) → ICurrentUser scoped ter-inject benar per pesan.
public static class InventoryInfrastructureExtensions
{
    public static IServiceCollection AddInventoryInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddAuditableEntityInterceptor();
        services.AddDbContext<InventoryDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", InventoryDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<InventoryDbContext>());

        services.AddTransactionalMessaging();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IPutawayTaskRepository, PutawayTaskRepository>();
        services.AddScoped<GoodsReceiptConfirmedConsumer>();
        services.AddSingleton<InventoryIntegrationEventDispatcher>();
        return services;
    }
}
