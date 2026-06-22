using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;
using Wms.BuildingBlocks.Infrastructure.Persistence;

namespace Wms.Auth.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Role (DDD; ADR-0010/0012)
// Why: Add/GetById dari EfRepository. GetActiveByIds = jalur MINT token — per-id EQUALITY (bukan Contains,
// yang bisa gagal translate untuk key ber-value-converter; N role per user kecil). Global soft-delete filter
// menjamin HANYA role aktif terambil (ADR-0012).
internal sealed class RoleRepository(AuthDbContext db)
    : EfRepository<Role, RoleId, AuthDbContext>(db), IRoleRepository
{
    public async Task<IReadOnlyList<Role>> GetActiveByIdsAsync(
        IEnumerable<RoleId> ids, CancellationToken cancellationToken = default)
    {
        var roles = new List<Role>();
        foreach (var id in ids.Distinct())
        {
            // global filter aktif → hanya role IsActive yang terambil (ADR-0012)
            var role = await DbSet.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (role is not null)
                roles.Add(role);
        }

        return roles;
    }
}
