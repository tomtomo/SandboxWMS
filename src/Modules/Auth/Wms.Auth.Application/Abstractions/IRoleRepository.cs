using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Auth.Domain;

namespace Wms.Auth.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Role; Add/GetById dari IRepository + claim-source read
// Why: GetActiveByIds = jalur MINT token (Login/Refresh) mengumpulkan permission code dari role user. HANYA
// role AKTIF (IsActive filter ADR-0012 — permission dari role non-aktif tak boleh bocor ke JWT self-contained).
// Filter ditegakkan global query filter di DbContext.
public interface IRoleRepository : IRepository<Role, RoleId>
{
    // What: resolve role AKTIF by ids untuk merakit claim (IsActive filter di jalur mint, ADR-0012)
    Task<IReadOnlyList<Role>> GetActiveByIdsAsync(
        IEnumerable<RoleId> ids, CancellationToken cancellationToken = default);
}
