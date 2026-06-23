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
    // PageSize 0 → TotalPages 0 (guard divide-by-zero); HasNextPage mirror PagedResult<T> BuildingBlocks
    // (UF-33 — cegah drift formula wire-vs-domain). Page 1-based.
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
