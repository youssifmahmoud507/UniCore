using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;

namespace UniCore.UnitTests.Common
{
    public class SpecificationTests
    {
        private sealed class Item
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private sealed class ActiveItemsSpec : Specification<Item>
        {
            public ActiveItemsSpec(PaginationQuery? paging = null, bool tracking = false)
            {
                SetCriteria(i => i.Id > 0);
                ApplyOrderBy(i => i.Name);
                AddInclude("Children");

                if (paging is not null) ApplyPaging(paging);
                if (tracking) UseTracking();
            }
        }

        [Fact]
        public void Spec_exposes_criteria_ordering_and_includes()
        {
            var spec = new ActiveItemsSpec();

            Assert.NotNull(spec.Criteria);
            Assert.NotNull(spec.OrderBy);
            Assert.Null(spec.OrderByDescending);
            Assert.Contains("Children", spec.IncludeStrings);
            Assert.True(spec.Criteria!.Compile()(new Item { Id = 1 }));
            Assert.False(spec.Criteria.Compile()(new Item { Id = 0 }));
        }

        [Fact]
        public void Spec_is_no_tracking_by_default_and_tracking_on_request()
        {
            Assert.True(new ActiveItemsSpec().AsNoTracking);
            Assert.False(new ActiveItemsSpec(tracking: true).AsNoTracking);
        }

        [Fact]
        public void ApplyPaging_sets_skip_and_take()
        {
            var spec = new ActiveItemsSpec(new PaginationQuery(3, 10));

            Assert.Equal(20, spec.Skip);
            Assert.Equal(10, spec.Take);
        }

        [Fact]
        public void Spec_without_paging_has_no_skip_or_take()
        {
            var spec = new ActiveItemsSpec();

            Assert.Null(spec.Skip);
            Assert.Null(spec.Take);
        }
    }
}
