using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Notification.Handlers;
using Wms.Notification.Messaging;
using Wms.Notification.Persistence;
using Wms.Notification.Subscriptions;
using Wms.Notification.Worker;

namespace Wms.Notification.DependencyInjection;

// What: composition modul Notification (collapsed) — AddNotification pattern (blueprint §3)
// Why: host cukup AddNotification(connStr). DbContext + map DbContext→NotificationDbContext (rail generik
// bekerja atas DbContext ambient, FF#4/#6) + transactional messaging (UoW/Inbox) + enqueuer + notifier +
// dispatcher + worker. PURE CONSUMER: TANPA AuditableEntityInterceptor (aggregate plain, di-author SYSTEM)
// & TANPA MediatR (notifier = plain class via dispatcher). gRPC directory adapter (Auth/MasterData) di-wire
// TERPISAH di host (AddNotificationDirectories) supaya test bisa stub.
// How: AddDbContext (snake_case + migrations-history schema "notification"); AddScoped<DbContext> delegasi
// (UoW/InboxGuard/enqueuer berbagi instance DbContext SAMA → commit delivery + Inbox atomic); notifier/enqueuer
// scoped; dispatcher singleton (IServiceScopeFactory). Worker singleton + hosted (resolvable langsung utk test).
public static class NotificationExtensions
{
    public static IServiceCollection AddNotification(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<NotificationDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", NotificationDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddTransactionalMessaging();

        services.AddScoped<NotificationEnqueuer>();
        services.AddScoped<GoodsReceiptConfirmedNotifier>();
        services.AddScoped<PickingCompletedNotifier>();
        services.AddScoped<StockAllocationShortfallNotifier>();   // ADR-0034
        services.AddSingleton<NotificationIntegrationEventDispatcher>();

        // What: worker async (BackgroundService) — singleton + hosted service
        // Why: didaftarkan sbg singleton AGAR integration test bisa resolve & invoke ProcessOnceAsync
        // langsung (deterministik); hosted service membungkus singleton yang sama untuk loop produksi.
        services.AddSingleton<NotificationDeliveryOptions>();
        services.AddSingleton<NotificationDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<NotificationDispatcher>());
        return services;
    }
}
