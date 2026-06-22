using Wms.Auth.Domain;

namespace Wms.Auth.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side RefreshToken (impl EF di Infrastructure)
// Why: GetByHash = jalur Refresh/Logout (token di-query BY HASH tiap refresh, ADR-0016 — token mentah
// tak pernah disimpan). GetById = walk rotation chain saat cascade reuse-detection (mengikuti
// ReplacedByTokenId). Mengembalikan aggregate TRACKED → Rotate/Revoke ter-persist saat SaveChanges.
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetByIdAsync(RefreshTokenId id, CancellationToken cancellationToken = default);
}
