using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Auth.Domain;

namespace Wms.Auth.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side RefreshToken; Add/GetById dari IRepository + lookup hash
// Why: GetByHash = jalur Refresh/Logout (token di-query BY HASH tiap refresh, ADR-0016 — token mentah tak
// pernah disimpan). GetById (base) = walk rotation chain saat cascade reuse-detection (ReplacedByTokenId).
// Mengembalikan aggregate TRACKED → Rotate/Revoke ter-persist saat SaveChanges.
public interface IRefreshTokenRepository : IRepository<RefreshToken, RefreshTokenId>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
