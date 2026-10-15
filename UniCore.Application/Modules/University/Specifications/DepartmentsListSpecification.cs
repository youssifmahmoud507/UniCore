using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class DepartmentsListSpecification : Specification<Department>
    {
        public static readonly string[] SortFields = ["code", "name"];

        public DepartmentsListSpecification(PaginationQuery query, string? search, bool? isActive, Guid? facultyId, SortOption sort)
        {
            SetCriteria(d => (isActive == null || d.IsActive == isActive)
                && (facultyId == null || d.FacultyId == facultyId)
                && (search == null || d.Code.StartsWith(search) || d.Name.Contains(search)));

            switch (sort.Field)
            {
                case "name":
                    if (sort.Descending) { ApplyOrderByDescending(d => d.Name); } else { ApplyOrderBy(d => d.Name); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(d => d.Code); } else { ApplyOrderBy(d => d.Code); }
                    break;
            }

            ApplyPaging(query);
        }
    }
}
