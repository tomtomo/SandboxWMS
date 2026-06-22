namespace Wms.BuildingBlocks.Application.Pagination;

// What: envelope hasil query paginated (read-side; standar enterprise)
// Why: pagination adalah cross-cutting concern di semua list endpoint — satu record mencegah drift
// kontrak antar-modul (Total vs TotalCount) DAN unbounded result set (Nygard, Release It! — stability
// antipattern). Page 1-based supaya konsisten user-facing.
// How: Reader/query handler mengembalikan PagedResult<TDto>; endpoint serialize sebagai 200 OK body.
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    // PageSize 0 → 0 (guard divide-by-zero; bukan exception — envelope tetap valid untuk empty query).
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
