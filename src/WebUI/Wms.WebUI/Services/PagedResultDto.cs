namespace Wms.WebUI.Services;

// What: envelope DTO pagination (selaras PagedResult<T> BuildingBlocks) — decoupled, tak ref module
// Why: WebUI mendeserialisasi respons endpoint paginated (Items + metadata) tanpa bergantung tipe modul.
// Reusable lintas client (Inbound/Reporting/Notification) saat rollout. Page 1-based.
public sealed record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public bool HasNextPage => Page * PageSize < TotalCount;

    public bool HasPreviousPage => Page > 1;
}
