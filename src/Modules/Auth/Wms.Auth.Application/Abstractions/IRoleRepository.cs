using Wms.Auth.Domain;

namespace Wms.Auth.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side Role + claim-source read (impl EF di Infrastructure)
// Why: GetActiveByIds = jalur MINT token (Login/Refresh) mengumpulkan permission code dari role user.
// HANYA role AKTIF yang dikembalikan (IsActive filter ADR-0012 — permission dari role non-aktif tak boleh
// bocor ke JWT self-contained). Filter ditegakkan global query filter di DbContext (lihat Infrastructure).
public interface IRoleRepository
{
    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default);

    // What: resolve role AKTIF by ids untuk merakit claim (IsActive filter di jalur mint, ADR-0012)
    Task<IReadOnlyList<Role>> GetActiveByIdsAsync(
        IEnumerable<RoleId> ids, CancellationToken cancellationToken = default);
}
