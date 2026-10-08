using UniCore.Application.Common.Pagination;

namespace UniCore.UnitTests.Common
{
    public class PaginationTests
    {
        [Fact]
        public void Defaults_are_page_1_size_20()
        {
            var q = new PaginationQuery();

            Assert.Equal(1, q.NormalizedPage);
            Assert.Equal(20, q.NormalizedPageSize);
            Assert.Equal(0, q.Skip);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-5, 1)]
        [InlineData(3, 3)]
        public void Page_is_clamped_to_at_least_1(int page, int expected)
            => Assert.Equal(expected, new PaginationQuery(page, 10).NormalizedPage);

        [Theory]
        [InlineData(0, 20)]
        [InlineData(-1, 20)]
        [InlineData(50, 50)]
        [InlineData(1000, 100)]
        public void PageSize_is_clamped_between_default_and_max(int size, int expected)
            => Assert.Equal(expected, new PaginationQuery(1, size).NormalizedPageSize);

        [Fact]
        public void Skip_is_computed_from_page_and_size()
            => Assert.Equal(40, new PaginationQuery(3, 20).Skip);

        [Fact]
        public void PagedResult_computes_pages_and_navigation_flags()
        {
            var query = new PaginationQuery(2, 10);
            var result = PagedResult<int>.Create(new[] { 1, 2, 3 }, query, totalCount: 25);

            Assert.Equal(3, result.TotalPages);
            Assert.True(result.HasPrevious);
            Assert.True(result.HasNext);
            Assert.Equal(2, result.Page);
            Assert.Equal(10, result.PageSize);
        }

        [Fact]
        public void PagedResult_last_page_has_no_next()
        {
            var result = PagedResult<int>.Create(Array.Empty<int>(), new PaginationQuery(3, 10), 25);

            Assert.False(result.HasNext);
        }
    }
}
