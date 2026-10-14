using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class FeeDefinitionsListSpecification : Specification<FeeDefinition>
    {
        public FeeDefinitionsListSpecification(PaginationQuery query, bool? isActive, string? search)
        {
            var hasSearch = !string.IsNullOrEmpty(search);
            var needle = search ?? string.Empty;
            var upper = needle.ToUpperInvariant();

            SetCriteria(d => (isActive == null || d.IsActive == isActive)
                && (!hasSearch || d.Code.StartsWith(upper) || d.Name.Contains(needle)));

            ApplyOrderBy(d => d.Code);
            ApplyPaging(query);
        }
    }



}
