using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Infrastructure.Persistence;

namespace UniCore.IntegrationTests.Persistence
{
    public class SpecificationEvaluatorTests
    {
        private sealed class Item
        {
            public int Id { get; set; }
        }

        private sealed class ItemsSpec : Specification<Item>
        {
            public ItemsSpec(bool descending = false, PaginationQuery? paging = null)
            {
                SetCriteria(i => i.Id > 1);

                if (descending)
                {
                    ApplyOrderByDescending(i => i.Id);
                }
                else
                {
                    ApplyOrderBy(i => i.Id);
                }

                if (paging is not null)
                {
                    ApplyPaging(paging);
                }
            }
        }

        private static IQueryable<Item> Data() => Enumerable.Range(1, 10).Select(i => new Item { Id = i }).AsQueryable();

        [Fact]
        public void Criteria_filters_items()
        {
            var result = SpecificationEvaluator.Apply(Data(), new ItemsSpec()).ToList();

            Assert.Equal(9, result.Count);
            Assert.DoesNotContain(result, i => i.Id == 1);
        }

        [Fact]
        public void Orders_ascending_and_descending()
        {
            var asc = SpecificationEvaluator.Apply(Data(), new ItemsSpec()).Select(i => i.Id).ToList();
            var desc = SpecificationEvaluator.Apply(Data(), new ItemsSpec(descending: true)).Select(i => i.Id).ToList();

            Assert.Equal(2, asc.First());
            Assert.Equal(10, desc.First());
        }

        [Fact]
        public void Paging_applies_skip_and_take()
        {
            var spec = new ItemsSpec(paging: new PaginationQuery(2, 3));

            var ids = SpecificationEvaluator.Apply(Data(), spec).Select(i => i.Id).ToList();

            Assert.Equal(new[] { 5, 6, 7 }, ids);
        }

        [Fact]
        public void ForCount_ignores_paging()
        {
            var spec = new ItemsSpec(paging: new PaginationQuery(2, 3));

            var count = SpecificationEvaluator.Apply(Data(), spec, forCount: true).Count();
            Assert.Equal(9, count);
        }
    }
}
