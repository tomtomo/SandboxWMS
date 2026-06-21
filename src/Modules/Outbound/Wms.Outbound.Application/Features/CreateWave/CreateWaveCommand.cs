using Wms.BuildingBlocks.Application.Messaging;

namespace Wms.Outbound.Application.Features.CreateWave;

// What: CQRS Command (ADR-0004) — SPV buat wave dari beberapa order (overview §C2)
// Why: mengelompokkan order untuk picking/dispatch bersama → emit WaveReleased. Mengembalikan id wave baru
// (server-generated) via Result<Guid>; AggregateId belum ada pra-create → BUKAN IAuditableCommand (created_by
// tetap di-stempel interceptor IAuditable). Marker // TODO-AUTH: Outbound.CreateWave dipasang di endpoint.
public sealed record CreateWaveCommand(IReadOnlyList<Guid> OrderIds) : ICommand<Guid>;
