using Wms.Auth.Application.Abstractions;
using Wms.Auth.Application.ReadModels;
using Wms.BuildingBlocks.Application.Caching;

namespace Wms.Auth.Infrastructure.Caching;

// What: Decorator (GoF) cache-aside di atas IAuthReader (ADR-0011, mirror MasterData 04a)
// Why: read-API Auth (User/Role/Permission) read-heavy & jarang berubah → bungkus reader EF dengan
// CACHE-ASIDE TTL-first: GET cache → HIT served; MISS → baca authority → POPULATE (TTL) → return.
// Decorator TRANSPARAN: gRPC service inject IAuthReader → menerima instance ini tanpa tahu ada cache.
// TTL-first (invalidasi event-driven DICATAT-TAK-AKTIF). Hanya hasil non-null di-cache (tak cache negative).
// How: per entity key "auth:{type}:{id}".
internal sealed class CachedAuthReader(IAuthReader inner, ICacheStore cache) : IAuthReader
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public async Task<UserReadModel?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = $"auth:user:{userId}";
        var cached = await cache.GetAsync<UserReadModel>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var user = await inner.GetUserAsync(userId, cancellationToken);
        if (user is not null)
            await cache.SetAsync(key, user, Ttl, cancellationToken);
        return user;
    }

    public async Task<RoleReadModel?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var key = $"auth:role:{roleId}";
        var cached = await cache.GetAsync<RoleReadModel>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var role = await inner.GetRoleAsync(roleId, cancellationToken);
        if (role is not null)
            await cache.SetAsync(key, role, Ttl, cancellationToken);
        return role;
    }

    public async Task<PermissionReadModel?> GetPermissionAsync(string code, CancellationToken cancellationToken = default)
    {
        var key = $"auth:permission:{code}";
        var cached = await cache.GetAsync<PermissionReadModel>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var permission = await inner.GetPermissionAsync(code, cancellationToken);
        if (permission is not null)
            await cache.SetAsync(key, permission, Ttl, cancellationToken);
        return permission;
    }
}
