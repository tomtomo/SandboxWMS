using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Application.Security;
using Wms.Auth.Infrastructure.Caching;
using Wms.Auth.Infrastructure.Persistence;
using Wms.Auth.Infrastructure.Security;
using Wms.BuildingBlocks.Application.Caching;
using Wms.BuildingBlocks.Infrastructure.Auditing;
using Wms.BuildingBlocks.Infrastructure.DependencyInjection;

namespace Wms.Auth.Infrastructure.DependencyInjection;

// What: composition modul Auth (Infrastructure) — AddXxxModule pattern (blueprint §3)
// Why: host cukup AddAuthInfrastructure(connStr). Mendaftarkan DbContext (schema auth + rail
// infrastructure) + IUnitOfWork (CRUD via AddTransactionalMessaging) + repos write-side + READ-PORT
// cache-aside DECORATOR (mirror MasterData 04a) + ADAPTER SECURITY (RS256 issuer + refresh generator) +
// seeder. ICacheStore & ISecretProvider di-wire host (Platform.Local). Options token/seed default
// di-register di sini (host boleh override via config — last-wins).
// How: AddDbContext UseNpgsql + snake_case + MigrationsHistory per-schema; issuer/generator SINGLETON
// (cache key/stateless); read-port via factory delegate (Decorator).
public static class AuthInfrastructureExtensions
{
    public static IServiceCollection AddAuthInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddAuditableEntityInterceptor();
        services.AddDbContext<AuthDbContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", AuthDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AuthDbContext>());

        // IUnitOfWork untuk slice Login/Refresh/Logout (outbox/inbox idle — Auth read-only ke core).
        services.AddTransactionalMessaging();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // What: read-port + cache-aside Decorator — IAuthReader = Cached(AuthReader EF)
        services.AddScoped<AuthReader>();
        services.AddScoped<IAuthReader>(sp =>
            new CachedAuthReader(
                sp.GetRequiredService<AuthReader>(),
                sp.GetRequiredService<ICacheStore>()));

        // What: adapter security — RS256 issuer (private key via ISecretProvider) + refresh generator.
        // SINGLETON: issuer cache signing key, generator stateless.
        services.AddSingleton<IAccessTokenIssuer, Rs256AccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        services.AddScoped<AuthSeeder>();

        // default options (host boleh override via config dengan registrasi setelah ini — last-wins)
        services.AddSingleton(new AuthTokenOptions());
        services.AddSingleton(new AuthSeedOptions());

        return services;
    }
}
