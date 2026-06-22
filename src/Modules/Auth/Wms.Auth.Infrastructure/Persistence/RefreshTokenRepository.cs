using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk RefreshToken (DDD; ADR-0016)
// How: GetByHash = lookup by unique index token_hash (refresh/logout); GetById = walk rotation chain
// (cascade reuse-detection). Return aggregate TRACKED → Rotate/Revoke ter-persist saat SaveChanges.
internal sealed class RefreshTokenRepository(AuthDbContext db) : IRefreshTokenRepository
{
    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        db.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task<RefreshToken?> GetByIdAsync(RefreshTokenId id, CancellationToken cancellationToken = default)
        => db.RefreshTokens.FirstOrDefaultAsync(token => token.Id == id, cancellationToken);
}
