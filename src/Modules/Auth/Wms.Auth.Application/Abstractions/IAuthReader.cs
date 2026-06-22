using Wms.Auth.Application.ReadModels;

namespace Wms.Auth.Application.Abstractions;

// What: Read-Port (reader-delegation; ADR-0011) — sisi-baca Auth yang dikonsumsi gRPC read-API
// Why: gRPC service ([*.Api]) delegasi ke port ini, BUKAN inject DbContext (dijaga FF#8) — boundary
// query terisolasi dari EF, cache-aside disisipkan sebagai DECORATOR atas port ini (mirror MasterData
// 04a, ADR-0011). Mengembalikan read-DTO (bypass aggregate, inti CQRS). null = NOT FOUND (no-throw,
// dipetakan NotFound di tepi). GetUser/GetRole hanya menyertakan permission dari role AKTIF (IsActive
// filter di jalur claim-source gRPC, ADR-0012).
// How: impl EF read-only di Infrastructure; decorator cache-aside membungkus (TTL-first, key auth:{type}:{id}).
public interface IAuthReader
{
    Task<UserReadModel?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RoleReadModel?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<PermissionReadModel?> GetPermissionAsync(string code, CancellationToken cancellationToken = default);
}
