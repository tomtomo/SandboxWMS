using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;

namespace Wms.Auth.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk User (DDD; ADR-0010)
// How: TANPA global filter — jalur Login memuat user Disabled/Locked untuk error seragam &
// RecordFailedLogin (status dicek di handler). GetByUsername/GetById return aggregate TRACKED.
internal sealed class UserRepository(AuthDbContext db) : IUserRepository
{
    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(user => user.Username == username, cancellationToken);

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default)
        => db.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
}
