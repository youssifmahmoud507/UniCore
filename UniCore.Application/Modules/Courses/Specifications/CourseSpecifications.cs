using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Courses;

namespace UniCore.Application.Modules.Courses.Specifications
{
    public sealed class CourseByCodeSpecification : Specification<Course>
    {
        public CourseByCodeSpecification(Guid departmentId, string code)
        {
            SetCriteria(c => c.DepartmentId == departmentId && c.Code == code);
        }
    }

    public sealed class CoursesListSpecification : Specification<Course>
    {
        public static readonly string[] SortFields = ["code", "title", "createdAt"];

        public CoursesListSpecification(PaginationQuery query, string? search, bool? isActive, Guid? departmentId, CourseType? courseType, SortOption sort)
        {
            SetCriteria(c => (isActive == null || c.IsActive == isActive)
                && (departmentId == null || c.DepartmentId == departmentId)
                && (courseType == null || c.CourseType == courseType)
                && (search == null || c.Code.StartsWith(search) || c.Title.Contains(search)));

            switch (sort.Field)
            {
                case "title":
                    if (sort.Descending) { ApplyOrderByDescending(c => c.Title); } else { ApplyOrderBy(c => c.Title); }
                    break;
                case "createdAt":
                    if (sort.Descending) { ApplyOrderByDescending(c => c.CreatedAt); } else { ApplyOrderBy(c => c.CreatedAt); }
                    break;
                default:
                    if (sort.Descending) { ApplyOrderByDescending(c => c.Code); } else { ApplyOrderBy(c => c.Code); }
                    break;
            }

            ApplyPaging(query);
        }
    }

    // All versions of a course, newest first.
    public sealed class VersionsOfCourseSpecification : Specification<CourseVersion>
    {
        public VersionsOfCourseSpecification(Guid courseId)
        {
            SetCriteria(v => v.CourseId == courseId);
            ApplyOrderByDescending(v => v.VersionNumber);
        }
    }

    public sealed class LatestVersionOfCourseSpecification : Specification<CourseVersion>
    {
        public LatestVersionOfCourseSpecification(Guid courseId)
        {
            SetCriteria(v => v.CourseId == courseId);
            ApplyOrderByDescending(v => v.VersionNumber);
            ApplyPaging(new PaginationQuery(1, 1));
        }
    }

    public sealed class CourseVersionByStatusSpecification : Specification<CourseVersion>
    {
        public CourseVersionByStatusSpecification(Guid courseId, CourseVersionStatus status, bool tracking = false)
        {
            SetCriteria(v => v.CourseId == courseId && v.Status == status);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // A Retired version that ends on or after the date: a new version starting then would overlap it.
    public sealed class RetiredVersionEndingFromSpecification : Specification<CourseVersion>
    {
        public RetiredVersionEndingFromSpecification(Guid courseId, DateOnly from)
        {
            SetCriteria(v => v.CourseId == courseId && v.Status == CourseVersionStatus.Retired && v.EffectiveTo != null && v.EffectiveTo >= from);
        }
    }

    // The newest version whose window covers the date (never a Draft).
    public sealed class CourseEffectiveVersionSpecification : Specification<CourseVersion>
    {
        public CourseEffectiveVersionSpecification(Guid courseId, DateOnly date)
        {
            SetCriteria(v => v.CourseId == courseId && v.Status != CourseVersionStatus.Draft
                && v.EffectiveFrom <= date && (v.EffectiveTo == null || v.EffectiveTo >= date));
            ApplyOrderByDescending(v => v.EffectiveFrom);
        }
    }
}

namespace UniCore.Application.Modules.Courses.Specifications
{
    // One version, tracked, so the handler can change it.
    public sealed class CourseVersionByIdSpecification : Specification<CourseVersion>
    {
        public CourseVersionByIdSpecification(Guid versionId)
        {
            SetCriteria(v => v.Id == versionId);
            UseTracking();
        }
    }
}
