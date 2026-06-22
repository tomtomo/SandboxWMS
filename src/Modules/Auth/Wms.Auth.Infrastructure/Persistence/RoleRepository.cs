using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Role (DDD; ADR-0010)
// How: GetActiveByIds memuat per-id via EQUALITY (r.Id == id) — bukan Contains, yang bisa gagal translate
// untuk key ber-value-converter; N role per user kecil (N+1 dapat diabaikan). Global soft-delete filter
// menjamin HANYA role aktif terambil (IsActive filter di jalur mint, ADR-0012).
internal sealed class RoleRepository(AuthDbContext db) : IRoleRepository
{
    public Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        db.Roles.Add(role);
        return Task.CompletedTask;
    }

    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default)
        => db.Roles.FirstOrDefaultAsync(role => role.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetActiveByIdsAsync(
        IEnumerable<RoleId> ids, CancellationToken cancellationToken = default)
    {
        var roles = new List<Role>();
        foreach (var id in ids.Distinct())
        {
            // global filter aktif → hanya role IsActive yang terambil (ADR-0012)
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (role is not null)
                roles.Add(role);
        }

        return roles;
    }
}
