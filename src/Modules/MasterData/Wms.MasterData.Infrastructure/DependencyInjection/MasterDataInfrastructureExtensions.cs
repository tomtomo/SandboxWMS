using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Caching;
using Wms.BuildingBlocks.Infrastructure.Auditing;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Infrastructure.Caching;
using Wms.MasterData.Infrastructure.Persistence;

namespace Wms.MasterData.Infrastructure.DependencyInjection;

// What: composition modul MasterData (Infrastructure) — AddXxxModule pattern (blueprint §3)
// Why: host cukup AddMasterDataInfrastructure(connStr). Mendaftarkan DbContext (schema masterdata +
// rail infrastructure) + IUnitOfWork (CRUD, via AddTransactionalMessaging) + repos write-side + READ-PORT
// dengan cache-aside DECORATOR (CachedMasterDataReader membungkus MasterDataReader EF). ICacheStore
// di-wire host (Platform.Local AddLocalCaching). Interceptor audit di-resolve dari sp (ICurrentUser
// scoped ter-inject benar).
// How: AddDbContext UseNpgsql + snake_case + MigrationsHistory per-schema; AddScoped DbContext delegasi
// (rail generik atas DbContext ambient, FF#10); read-port via factory delegate (Decorator).
public static class MasterDataInfrastructureExtensions
{
    public static IServiceCollection AddMasterDataInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddAuditableEntityInterceptor();
        services.AddDbContext<MasterDataDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<MasterDataDbContext>());

        // IUnitOfWork untuk CRUD slices (outbox/inbox primitives idle — MasterData read-only ke core).
        services.AddTransactionalMessaging();

        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        // What: read-port + cache-aside Decorator — IMasterDataReader = Cached(MasterDataReader EF)
        services.AddScoped<MasterDataReader>();
        services.AddScoped<IMasterDataReader>(sp =>
            new CachedMasterDataReader(
                sp.GetRequiredService<MasterDataReader>(),
                sp.GetRequiredService<ICacheStore>()));

        return services;
    }
}
