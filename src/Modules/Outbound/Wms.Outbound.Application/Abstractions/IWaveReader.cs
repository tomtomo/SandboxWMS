using Wms.BuildingBlocks.Application.Pagination;
using Wms.Outbound.Application.ReadModels;

namespace Wms.Outbound.Application.Abstractions;

// What: Read-Port (CQRS read-side + Ports & Adapters; ADR-0004/0011) — sisi-baca Wave
// Why: query list/detail mem-bypass aggregate/repository (baca langsung → read-DTO) sesuai CQRS;
// endpoint REST (*.Api) bergantung ke abstraksi ini, BUKAN DbContext (FF#8) — impl EF di Infrastructure.
// List paginated (PagedResult). OrderCount/LineCount dirakit dari OrderIds (primitive collection)
// + follow-up query OrderLines lintas order wave.
// How: ListAsync(status optional filter, page 1-based); GetAsync(waveId) → null bila tak ada.
public interface IWaveReader
{
    Task<PagedResult<WaveSummary>> ListAsync(
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<WaveDetail?> GetAsync(Guid waveId, CancellationToken cancellationToken = default);
}
