using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Calendar;

namespace UniCore.Application.Modules.Calendar.Specifications
{
    public sealed class AcademicYearsListSpecification : Specification<AcademicYear>
    {
        public static readonly string[] SortFields = ["startDate", "name"];

        public AcademicYearsListSpecification(PaginationQuery query, string? search, AcademicYearStatus? status, bool? isCurrent, SortOption sort)
        {
            SetCriteria(y => (status == null || y.Status == status)
                && (isCurrent == null || y.IsCurrent == isCurrent)
                && (search == null || y.Name.Contains(search)));

            if (sort.Field == "name")
            {
                if (sort.Descending) { ApplyOrderByDescending(y => y.Name); } else { ApplyOrderBy(y => y.Name); }
            }
            else
            {
                if (sort.Descending) { ApplyOrderByDescending(y => y.StartDate); } else { ApplyOrderBy(y => y.StartDate); }
            }

            ApplyPaging(query);
        }
    }

    // Years whose dates overlap [start, end], not counting the year `excludeId` (the one being edited).
    public sealed class OverlappingYearsSpecification : Specification<AcademicYear>
    {
        public OverlappingYearsSpecification(DateOnly start, DateOnly end, Guid? excludeId)
        {
            SetCriteria(y => y.StartDate <= end && y.EndDate >= start && (excludeId == null || y.Id != excludeId));
        }
    }

    public sealed class CurrentYearSpecification : Specification<AcademicYear>
    {
        public CurrentYearSpecification(Guid? excludeId)
        {
            SetCriteria(y => y.IsCurrent && (excludeId == null || y.Id != excludeId));
        }
    }

    public sealed class AcademicTermsListSpecification : Specification<AcademicTerm>
    {
        public static readonly string[] SortFields = ["sortOrder", "code", "name"];

        public AcademicTermsListSpecification(PaginationQuery query, string? search, bool? isActive, SortOption sort)
        {
            SetCriteria(t => (isActive == null || t.IsActive == isActive)
                && (search == null || t.Code.StartsWith(search) || t.Name.Contains(search)));

            switch (sort.Field)
            {
                case "code":
                    if (sort.Descending) { ApplyOrderByDescending(t => t.Code); } else { ApplyOrderBy(t => t.Code); }
                    break;
                case "name":
                    if (sort.Descending) { ApplyOrderByDescending(t => t.Name); } else { ApplyOrderBy(t => t.Name); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(t => t.SortOrder); } else { ApplyOrderBy(t => t.SortOrder); }
                    break;
            }

            ApplyPaging(query);
        }
    }

    public sealed class SemestersListSpecification : Specification<AcademicSemester>
    {
        public static readonly string[] SortFields = ["startDate", "endDate", "status"];

        public SemestersListSpecification(PaginationQuery query, Guid? academicYearId, Guid? academicTermId, SemesterStatus? status, SortOption sort)
        {
            SetCriteria(s => (academicYearId == null || s.AcademicYearId == academicYearId)
                && (academicTermId == null || s.AcademicTermId == academicTermId)
                && (status == null || s.Status == status));

            switch (sort.Field)
            {
                case "endDate":
                    if (sort.Descending) { ApplyOrderByDescending(s => s.EndDate); } else { ApplyOrderBy(s => s.EndDate); }
                    break;
                case "status":
                    if (sort.Descending) { ApplyOrderByDescending(s => s.Status); } else { ApplyOrderBy(s => s.Status); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(s => s.StartDate); } else { ApplyOrderBy(s => s.StartDate); }
                    break;
            }

            ApplyPaging(query);
        }
    }

    public sealed class SemesterOfYearAndTermSpecification : Specification<AcademicSemester>
    {
        public SemesterOfYearAndTermSpecification(Guid academicYearId, Guid academicTermId)
        {
            SetCriteria(s => s.AcademicYearId == academicYearId && s.AcademicTermId == academicTermId);
        }
    }

    public sealed class OverlappingSemestersSpecification : Specification<AcademicSemester>
    {
        public OverlappingSemestersSpecification(Guid academicYearId, DateOnly start, DateOnly end, Guid? excludeId)
        {
            SetCriteria(s => s.AcademicYearId == academicYearId && s.StartDate <= end && s.EndDate >= start && (excludeId == null || s.Id != excludeId));
        }
    }

    // Semesters of the year that would fall outside [start, end].
    public sealed class SemestersOutsideRangeSpecification : Specification<AcademicSemester>
    {
        public SemestersOutsideRangeSpecification(Guid academicYearId, DateOnly start, DateOnly end)
        {
            SetCriteria(s => s.AcademicYearId == academicYearId && (s.StartDate < start || s.EndDate > end));
        }
    }

    public sealed class OpenSemestersOfYearSpecification : Specification<AcademicSemester>
    {
        public OpenSemestersOfYearSpecification(Guid academicYearId)
        {
            SetCriteria(s => s.AcademicYearId == academicYearId && s.Status != SemesterStatus.Closed);
        }
    }

    // Semesters of one year whose range contains the date.
    public sealed class SemestersContainingDateSpecification : Specification<AcademicSemester>
    {
        public SemestersContainingDateSpecification(Guid academicYearId, DateOnly date)
        {
            SetCriteria(s => s.AcademicYearId == academicYearId && s.StartDate <= date && s.EndDate >= date);
        }
    }

    public sealed class AcademicLevelsListSpecification : Specification<AcademicLevel>
    {
        public static readonly string[] SortFields = ["order", "name"];

        public AcademicLevelsListSpecification(PaginationQuery query, string? search, bool? isActive, Guid? academicProgramId, SortOption sort)
        {
            SetCriteria(l => (isActive == null || l.IsActive == isActive)
                && (academicProgramId == null || l.AcademicProgramId == academicProgramId)
                && (search == null || l.Name.Contains(search)));

            if (sort.Field == "name")
            {
                if (sort.Descending) { ApplyOrderByDescending(l => l.Name); } else { ApplyOrderBy(l => l.Name); }
            }
            else
            {
                if (sort.Descending) { ApplyOrderByDescending(l => l.Order); } else { ApplyOrderBy(l => l.Order); }
            }

            ApplyPaging(query);
        }
    }
}
