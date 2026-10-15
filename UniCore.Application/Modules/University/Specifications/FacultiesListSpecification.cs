using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class FacultiesListSpecification : Specification<Faculty>
    {
        public static readonly string[] SortFields = ["code", "name", "createdAt"];

        public FacultiesListSpecification(PaginationQuery query, string? search, bool? isActive, Guid? universityId, SortOption sort)
        {
            SetCriteria(f => (isActive == null || f.IsActive == isActive)
                && (universityId == null || f.UniversityId == universityId)
                && (search == null || f.Code.StartsWith(search) || f.Name.Contains(search)));

            switch (sort.Field)
            {
                case "name":
                    if (sort.Descending) { ApplyOrderByDescending(f => f.Name); } else { ApplyOrderBy(f => f.Name); }
                    break;
                case "createdAt":
                    if (sort.Descending) { ApplyOrderByDescending(f => f.CreatedAt); } else { ApplyOrderBy(f => f.CreatedAt); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(f => f.Code); } else { ApplyOrderBy(f => f.Code); }
                    break;
            }

            ApplyPaging(query);
        }
    }
}
