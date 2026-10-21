using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Curriculum;

namespace UniCore.Application.Modules.Curriculum.Specifications
{
    public sealed class StudyPlanByCodeSpecification : Specification<StudyPlan>
    {
        public StudyPlanByCodeSpecification(Guid programId, string code)
        {
            SetCriteria(p => p.AcademicProgramId == programId && p.Code == code);
        }
    }

    public sealed class StudyPlansListSpecification : Specification<StudyPlan>
    {
        public static readonly string[] SortFields = ["code", "name", "createdAt"];

        public StudyPlansListSpecification(PaginationQuery query, string? search, bool? isActive, Guid? programId, SortOption sort)
        {
            SetCriteria(p => (isActive == null || p.IsActive == isActive)
                && (programId == null || p.AcademicProgramId == programId)
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

    // One version with its courses and requirements.
    public sealed class VersionWithDetailsSpecification : Specification<StudyPlanVersion>
    {
        public VersionWithDetailsSpecification(Guid versionId, bool tracking)
        {
            SetCriteria(v => v.Id == versionId);
            AddInclude(v => v.Courses);
            AddInclude(v => v.Requirements);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // All versions of a plan, newest first (without their courses).
    public sealed class VersionsOfPlanSpecification : Specification<StudyPlanVersion>
    {
        public VersionsOfPlanSpecification(Guid planId)
        {
            SetCriteria(v => v.StudyPlanId == planId);
            ApplyOrderByDescending(v => v.VersionNumber);
        }
    }

    public sealed class LatestVersionOfPlanSpecification : Specification<StudyPlanVersion>
    {
        public LatestVersionOfPlanSpecification(Guid planId)
        {
            SetCriteria(v => v.StudyPlanId == planId);
            ApplyOrderByDescending(v => v.VersionNumber);
            ApplyPaging(new PaginationQuery(1, 1));
        }
    }

    public sealed class VersionByStatusSpecification : Specification<StudyPlanVersion>
    {
        public VersionByStatusSpecification(Guid planId, StudyPlanVersionStatus status, bool tracking = false)
        {
            SetCriteria(v => v.StudyPlanId == planId && v.Status == status);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // The newest version whose window covers the date (never a Draft).
    public sealed class EffectiveVersionSpecification : Specification<StudyPlanVersion>
    {
        public EffectiveVersionSpecification(Guid planId, DateOnly date)
        {
            SetCriteria(v => v.StudyPlanId == planId && v.Status != StudyPlanVersionStatus.Draft
                && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date));
            ApplyOrderByDescending(v => v.EffectiveFrom);
        }
    }
}
