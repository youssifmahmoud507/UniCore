using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class ScholarshipsListSpecification : Specification<Scholarship>
    {
        public ScholarshipsListSpecification(PaginationQuery query, bool? isActive, string? search)
        {
            var hasSearch = !string.IsNullOrEmpty(search);
            var needle = search ?? string.Empty;
            var upper = needle.ToUpperInvariant();

            SetCriteria(s => (isActive == null || s.IsActive == isActive)
                && (!hasSearch || s.Code.StartsWith(upper) || s.Name.Contains(needle)));

            ApplyOrderBy(s => s.Code);
            ApplyPaging(query);
        }
    }






}
