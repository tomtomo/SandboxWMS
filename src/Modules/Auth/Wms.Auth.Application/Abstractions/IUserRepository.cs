using Wms.Auth.Domain;

namespace Wms.Auth.Application.Abstractions;

// What: Repository Pattern (DDD) — port write-side User (impl EF di Infrastructure)
// Why: slice Login/admin menulis & memuat User via abstraksi ini tanpa tahu EF (DIP, FF#5); commit
// dipisah ke IUnitOfWork. GetByUsername = jalur Login (lookup kredensial); GetById = jalur Refresh
// (resolve pemilik token). Mengembalikan aggregate TRACKED → RecordFailedLogin/SuccessfulLogin ter-persist.
public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);
}
