namespace Wms.BuildingBlocks.Application.Pagination;

// What: pagination input clamp (defensive read-side; DRY-of-knowledge)
// Why: aturan "page ≥ 1, pageSize 1..100 (default 20)" adalah SATU pengetahuan yang sebelumnya
// diduplikasi di tiap reader/endpoint (drift-prone) — disatukan agar batas unbounded result set
// (Nygard, Release It! — stability antipattern) dijaga konsisten lintas modul. Page 1-based, selaras
// PagedResult<T>. Default/Max sebagai const = satu sumber bila batas perlu disetel.
// How: factory murni (int? → tuple) — call-site: var (page, size) = PageRequest.From(page, size).
public static class PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) From(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
}
