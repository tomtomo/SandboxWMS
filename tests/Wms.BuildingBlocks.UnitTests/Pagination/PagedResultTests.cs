using Wms.BuildingBlocks.Application.Pagination;

namespace Wms.BuildingBlocks.UnitTests.Pagination;

// What: unit test computed props PagedResult (kontrak pagination; standar enterprise)
// Why: TotalPages/HasNextPage/HasPreviousPage adalah satu-satunya logika di envelope ini — wajib benar
// & bebas divide-by-zero saat PageSize 0 (guard). Page 1-based (user-facing).
public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(20, 10, 2)]
    [InlineData(25, 10, 3)]
    public void TotalPages_is_ceiling_of_total_over_page_size(int totalCount, int pageSize, int expected)
        => Assert.Equal(expected, Page(page: 1, pageSize, totalCount).TotalPages);

    [Fact]
    public void TotalPages_is_zero_when_page_size_zero()
        => Assert.Equal(0, Page(page: 1, pageSize: 0, totalCount: 100).TotalPages);

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void HasNextPage_true_until_last_page(int page, bool expected)
        => Assert.Equal(expected, Page(page, pageSize: 10, totalCount: 25).HasNextPage); // 25/10 → 3 pages

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void HasPreviousPage_true_after_first_page(int page, bool expected)
        => Assert.Equal(expected, Page(page, pageSize: 10, totalCount: 25).HasPreviousPage);

    private static PagedResult<string> Page(int page, int pageSize, int totalCount)
        => new([], page, pageSize, totalCount);
}
