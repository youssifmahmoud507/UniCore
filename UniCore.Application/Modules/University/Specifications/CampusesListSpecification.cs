using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class CampusesListSpecification : Specification<Campus>
    {
        public static readonly string[] SortFields = ["code", "name"];

        public CampusesListSpecification(PaginationQuery query, string? search, bool? isActive, Guid? universityId, SortOption sort)
        {
            SetCriteria(c => (isActive == null || c.IsActive == isActive)
                && (universityId == null || c.UniversityId == universityId)
                && (search == null || c.Code.StartsWith(search) || c.Name.Contains(search)));

            switch (sort.Field)
            {
                case "name":
                    if (sort.Descending) { ApplyOrderByDescending(c => c.Name); } else { ApplyOrderBy(c => c.Name); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(c => c.Code); } else { ApplyOrderBy(c => c.Code); }
                    break;
            }

            ApplyPaging(query);
        }
    }
}
