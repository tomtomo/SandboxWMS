using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;
using Wms.BuildingBlocks.Infrastructure.Persistence;

namespace Wms.Auth.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk RefreshToken (DDD; ADR-0016)
// Why: Add/GetById dari EfRepository. GetByHash = lookup by unique index token_hash (refresh/logout; token
// mentah tak pernah disimpan, ADR-0016). Mengembalikan aggregate TRACKED → Rotate/Revoke ter-persist.
internal sealed class RefreshTokenRepository(AuthDbContext db)
    : EfRepository<RefreshToken, RefreshTokenId, AuthDbContext>(db), IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
}
