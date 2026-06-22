using Wms.BuildingBlocks.Application.Abstractions;
using Wms.Auth.Domain;

namespace Wms.Auth.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side User; Add/GetById dari IRepository + lookup domain
// Why: GetByUsername = jalur Login (lookup kredensial); GetById (base) = jalur Refresh (resolve pemilik
// token). Mengembalikan aggregate TRACKED → RecordFailedLogin/SuccessfulLogin ter-persist.
public interface IUserRepository : IRepository<User, UserId>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}
