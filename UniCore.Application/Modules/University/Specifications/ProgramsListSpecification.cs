using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class ProgramsListSpecification : Specification<AcademicProgram>
    {
        public static readonly string[] SortFields = ["code", "name", "createdAt"];

        public ProgramsListSpecification(PaginationQuery query, string? search, bool? isActive, Guid? departmentId, DegreeType? degreeType, SortOption sort)
        {
            SetCriteria(p => (isActive == null || p.IsActive == isActive)
                && (departmentId == null || p.DepartmentId == departmentId)
                && (degreeType == null || p.DegreeType == degreeType)
                && (search == null || p.Code.StartsWith(search) || p.Name.Contains(search)));

            switch (sort.Field)
            {
                case "name":
                    if (sort.Descending) { ApplyOrderByDescending(p => p.Name); } else { ApplyOrderBy(p => p.Name); }
                    break;
                case "createdAt":
                    if (sort.Descending) { ApplyOrderByDescending(p => p.CreatedAt); } else { ApplyOrderBy(p => p.CreatedAt); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(p => p.Code); } else { ApplyOrderBy(p => p.Code); }
                    break;
            }

            ApplyPaging(query);
        }
    }
}
