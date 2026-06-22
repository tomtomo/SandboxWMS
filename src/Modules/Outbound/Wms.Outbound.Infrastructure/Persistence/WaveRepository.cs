using Wms.BuildingBlocks.Infrastructure.Persistence;
using Wms.Outbound.Application.Abstractions;
using Wms.Outbound.Domain;

namespace Wms.Outbound.Infrastructure.Persistence;

// What: Repository Pattern impl (EF Core) untuk Wave — Add/GetById dari EfRepository. Query tracked agar
// transisi (AttachPickingTasks/MarkReady/Dispatch) ter-flush saat SaveChanges.
internal sealed class WaveRepository(OutboundDbContext db)
    : EfRepository<Wave, WaveId, OutboundDbContext>(db), IWaveRepository;
