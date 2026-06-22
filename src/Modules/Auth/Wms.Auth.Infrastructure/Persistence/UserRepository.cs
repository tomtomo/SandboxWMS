using Microsoft.EntityFrameworkCore;
using Wms.Auth.Application.Abstractions;
using Wms.Auth.Domain;
using Wms.BuildingBlocks.Infrastructure.Persistence;

namespace Wms.Auth.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk User (DDD; ADR-0010)
// Why: Add/GetById dari EfRepository (hapus boilerplate). GetByUsername = query domain jalur Login. User
// TANPA global filter (memuat Disabled/Locked untuk error seragam & RecordFailedLogin; status dicek handler).
internal sealed class UserRepository(AuthDbContext db)
    : EfRepository<User, UserId, AuthDbContext>(db), IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => DbSet.FirstOrDefaultAsync(user => user.Username == username, cancellationToken);
}
