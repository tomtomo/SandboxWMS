using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.Inbound.Application.Abstractions;
using Wms.Inbound.Infrastructure.Persistence;

namespace Wms.Inbound.Infrastructure.DependencyInjection;

// What: composition modul Inbound (Infrastructure) — AddXxxModule pattern (blueprint §3)
// Why: host cukup AddInboundInfrastructure(connStr). Selain mendaftarkan DbContext modul,
// ia memetakan DbContext → InboundDbContext supaya komponen rail GENERIK (OutboxDispatcher,
// LocalDeadLetterStore) bekerja atas DbContext ambient tanpa mengenal tipe konkret modul —
// menjaga BuildingBlocks/Platform nol referensi ke Modules (FF#4/#6).
// How: AddDbContext<InboundDbContext> dgn UseNpgsql + snake_case + history table di schema
// modul; lalu AddScoped<DbContext> delegasi ke InboundDbContext (DB-per-service: satu
// DbContext per host → resolusi base DbContext tak ambigu).
public static class InboundInfrastructureExtensions
{
    public static IServiceCollection AddInboundInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<InboundDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", InboundDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<InboundDbContext>());

        // transactional messaging primitives (UoW + outbox writer + inbox guard) + repository.
        // Handler/validator/pipeline = AddInboundApplication (MediatR mendaftarkan handler).
        services.AddTransactionalMessaging();
        services.AddScoped<IGoodsReceiptRepository, GoodsReceiptRepository>();
        return services;
    }
}
