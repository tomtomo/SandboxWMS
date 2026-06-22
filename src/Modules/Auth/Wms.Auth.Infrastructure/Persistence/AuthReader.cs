using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Application.ReadModels;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence;

// What: Read-Port impl EF Core (reader-delegation; ADR-0011) — sisi-baca Auth untuk gRPC read-API
// Why: realisasi IAuthReader yang di-inject gRPC service (`.Api`) — gRPC TAK menyentuh DbContext langsung
// (FF#8). AsNoTracking (read-only). GetUser merakit role/permission code HANYA dari role AKTIF (global
// filter → IsActive filter di claim-source gRPC, ADR-0012). Materialize-then-map (bukan projection EF) →
// mapping read-model in-memory bebas batasan translasi strongly-typed id & koleksi JSON.
internal sealed class AuthReader(AuthDbContext db) : IAuthReader
{
    public async Task<UserReadModel?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == new UserId(userId), cancellationToken);
        if (user is null)
            return null;

        // gather active roles (per-id equality; global filter → aktif saja) — IsActive filter ADR-0012
        var roles = new List<Role>();
        foreach (var roleId in user.RoleIds)
        {
            var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);
            if (role is not null)
                roles.Add(role);
        }

        var roleCodes = roles.Select(role => role.Code).ToArray();
        var permissionCodes = roles.SelectMany(role => role.PermissionCodes).Distinct().ToArray();

        return new UserReadModel(
            user.Id.Value, user.Username, user.Email, user.Status.ToString(),
            roleCodes, permissionCodes, user.AssignedWarehouseIds.ToArray());
    }

    public async Task<RoleReadModel?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == new RoleId(roleId), cancellationToken);
        return role is null
            ? null
            : new RoleReadModel(role.Id.Value, role.Code, role.Name, role.PermissionCodes.ToArray());
    }

    public async Task<PermissionReadModel?> GetPermissionAsync(string code, CancellationToken cancellationToken = default)
    {
        var permission = await db.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == code, cancellationToken);
        return permission is null ? null : new PermissionReadModel(permission.Code, permission.Description);
    }
}
