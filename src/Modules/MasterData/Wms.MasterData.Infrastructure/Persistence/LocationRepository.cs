using Microsoft.EntityFrameworkCore;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Location (DDD; ADR-0010)
internal sealed class LocationRepository(MasterDataDbContext db) : ILocationRepository
{
    public Task AddAsync(Location location, CancellationToken cancellationToken = default)
    {
        db.Locations.Add(location);
        return Task.CompletedTask;
    }

    public Task<Location?> GetAsync(LocationId id, CancellationToken cancellationToken = default)
        => db.Locations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
}
