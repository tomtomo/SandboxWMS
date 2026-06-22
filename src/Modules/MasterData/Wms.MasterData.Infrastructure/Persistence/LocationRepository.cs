using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.MasterData.Application.Abstractions;
using Wms.MasterData.Domain;

namespace Wms.MasterData.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Location — Add/GetById dari EfRepository
internal sealed class LocationRepository(MasterDataDbContext db)
    : EfRepository<Location, LocationId, MasterDataDbContext>(db), ILocationRepository;
