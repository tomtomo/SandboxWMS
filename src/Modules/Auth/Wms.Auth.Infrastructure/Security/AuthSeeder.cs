using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Security;
using Wms.Auth.Domain;
using Wms.Auth.Infrastructure.Persistence;
using Wms.BuildingBlocks.Application.Security;

namespace Wms.Auth.Infrastructure.Security;

// What: idempotent seeder — Permission catalog + Admin role + 1 admin user (ADR-0012)
// Why: ADR-0012 menetapkan 1 admin default + planning catalog selama authZ deferred. Argon2id hashing
// butuh runtime (IPasswordHasher adapter) → seed di startup host, BUKAN HasData migration (yang statis,
// tak bisa hash). Idempoten: aman dijalankan tiap startup (cek eksistensi sebelum insert).
// How: tambah permission yang belum ada → Admin role (semua permission) → admin user (password ter-hash
// Argon2id) + assign Admin role. SaveChanges sekali.
public sealed class AuthSeeder(AuthDbContext db, IPasswordHasher passwordHasher, AuthSeedOptions options)
{
    public const string AdminRoleCode = "ADMIN";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken);
        var adminRole = await SeedAdminRoleAsync(cancellationToken);
        await SeedAdminUserAsync(adminRole, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await db.Permissions.Select(permission => permission.Id).ToListAsync(cancellationToken);
        foreach (var (code, description) in AuthPermissionCatalog.Permissions)
        {
            if (!existing.Contains(code))
                db.Permissions.Add(Permission.Create(code, description).Value);
        }
    }

    private async Task<Role> SeedAdminRoleAsync(CancellationToken cancellationToken)
    {
        // IncludeInactive: lihat role apa pun (idempotency tak boleh terhalang soft-delete filter)
        db.IncludeInactive = true;
        try
        {
            var existing = await db.Roles
                .FirstOrDefaultAsync(role => role.Code == AdminRoleCode, cancellationToken);
            if (existing is not null)
                return existing;
        }
        finally
        {
            db.IncludeInactive = false;
        }

        var adminRole = Role.Create(AdminRoleCode, "Administrator").Value;
        foreach (var (code, _) in AuthPermissionCatalog.Permissions)
            adminRole.AddPermission(code);

        db.Roles.Add(adminRole);
        return adminRole;
    }

    private async Task SeedAdminUserAsync(Role adminRole, CancellationToken cancellationToken)
    {
        var exists = await db.Users
            .AnyAsync(user => user.Username == options.AdminUsername, cancellationToken);
        if (exists)
            return;

        var passwordHash = passwordHasher.Hash(options.AdminPassword);
        var admin = User.Create(options.AdminUsername, options.AdminEmail, passwordHash).Value;
        admin.AssignRole(adminRole.Id);
        db.Users.Add(admin);
    }
}
