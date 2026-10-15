using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;

// The namespace UniCore.Application.Modules.University hides the class name, so the entity gets an alias.
using UniversityEntity = UniCore.Domain.Modules.University.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class UniversitiesListSpecification : Specification<UniversityEntity>
    {
        public static readonly string[] SortFields = ["code", "name", "createdAt"];

        // search is already trimmed and length-limited (ListQueryRules.NormalizeSearch). Null means "no filter".
        public UniversitiesListSpecification(PaginationQuery query, string? search, bool? isActive, SortOption sort)
        {
            SetCriteria(u => (isActive == null || u.IsActive == isActive)
                && (search == null || u.Code.StartsWith(search) || u.Name.Contains(search)));

            switch (sort.Field)
            {
                case "name":
                    if (sort.Descending) { ApplyOrderByDescending(u => u.Name); } else { ApplyOrderBy(u => u.Name); }
                    break;
                case "createdAt":
                    if (sort.Descending) { ApplyOrderByDescending(u => u.CreatedAt); } else { ApplyOrderBy(u => u.CreatedAt); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(u => u.Code); } else { ApplyOrderBy(u => u.Code); }
                    break;
            }

            ApplyPaging(query);
        }
    }
}
