using Microsoft.Extensions.DependencyInjection;
using Wms.BuildingBlocks.Application.Auditing;
using Wms.BuildingBlocks.Application.Caching;
using Wms.BuildingBlocks.Application.Messaging;
using Wms.BuildingBlocks.Application.Security;
using Wms.BuildingBlocks.Application.Storage;
using Wms.Platform.Local.Auditing;
using Wms.Platform.Local.Caching;
using Wms.Platform.Local.Messaging;
using Wms.Platform.Local.Security;
using Wms.Platform.Local.Storage;

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

    // What: composition adapter audit Local (port IAuditLogStore → tabel Postgres audit_log)
    // Why: dipisah dari messaging — audit operasional (ADR-0022) concern berbeda dari rail.
    // Scoped: butuh DbContext scoped; AuditLogBehavior me-resolve-nya di SCOPE BARU per write.
    public static IServiceCollection AddLocalAuditing(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogStore, LocalAuditLogStore>();
        return services;
    }

    // What: composition adapter object-storage Local (port IObjectStore → filesystem)
    // Why: byte attachment (ADR-0015) disimpan di filesystem lokal di bawah satu root. Adapter cloud
    // (Blob/GCS) menggantikannya tanpa sentuh core. Singleton: stateless, root path tetap.
    public static IServiceCollection AddLocalObjectStore(this IServiceCollection services, string rootPath)
    {
        services.AddSingleton<IObjectStore>(new LocalObjectStore(rootPath));
        return services;
    }

    // What: composition adapter cache Local (port ICacheStore → in-proc TTL; ADR-0011)
    // Why: cache-aside MasterData read-API butuh store; Local = in-memory. Singleton (state cache hidup
    // selama proses). Cloud (Redis/Memorystore) swap tanpa sentuh decorator. Dipakai 04a (MasterData)
    // & reuse 04b/04d.
    public static IServiceCollection AddLocalCaching(this IServiceCollection services)
    {
        services.AddSingleton<ICacheStore, InMemoryCacheStore>();
        return services;
    }

    // What: composition adapter service-token Local (port IServiceTokenProvider → trust-stub; ADR-0021)
    // Why: hop gRPC s2s butuh bearer; Local = token kosong (tak ada platform identity). Singleton
    // (stateless). Cloud (Managed Identity / SA OIDC) swap tanpa sentuh core/interceptor.
    public static IServiceCollection AddLocalServiceTokenProvider(this IServiceCollection services)
    {
        services.AddSingleton<IServiceTokenProvider, LocalServiceTokenProvider>();
        return services;
    }

    // What: composition adapter password-hasher Local (port IPasswordHasher → Argon2id; ADR-0016)
    // Why: jalur Login/seed butuh KDF; Local = Argon2id (Konscious). Singleton (precompute Sentinel sekali).
    // Cloud bisa swap adapter berbeda tanpa sentuh core (Hexagonal). Dipakai 04b (Auth).
    public static IServiceCollection AddLocalPasswordHasher(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        return services;
    }

    // What: composition adapter secret-provider Local (port ISecretProvider → env + ephemeral; ADR-0016)
    // Why: RS256 signing/public key di-resolve via port; Local = env var (`Secrets__{name}`) atau dev
    // keypair ephemeral. Singleton (keypair ephemeral hidup selama proses). Cloud (Key Vault/Secret Manager)
    // swap tanpa sentuh core/issuer. Dipakai 04b (Auth issuer + host JWT validation wiring).
    public static IServiceCollection AddLocalSecretProvider(this IServiceCollection services)
    {
        services.AddSingleton<ISecretProvider, LocalSecretProvider>();
        return services;
    }
}
