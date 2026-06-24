namespace Wms.Outbound.Application.ReadModels;

// What: read DTO (CQRS read-side; ADR-0004) — ringkasan Wave untuk list UI, decoupled dari
// aggregate: Status di-flatten ke string. OrderCount = jumlah order dalam wave (OrderIds primitive
// collection, dibaca in-memory). LineCount = total OrderLines lintas order wave (follow-up query).
public sealed record WaveSummary(
    Guid WaveId,
    string Status,
    int OrderCount,
    int LineCount);
