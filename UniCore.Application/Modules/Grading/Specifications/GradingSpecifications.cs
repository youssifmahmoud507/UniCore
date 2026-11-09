using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Grading;

namespace UniCore.Application.Modules.Grading.Specifications
{
    public sealed class GradeByIdSpecification : Specification<Grade>
    {
        public GradeByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(g => g.Id == id);
            AddInclude("Scores");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class GradeOfEnrollmentSpecification : Specification<Grade>
    {
        public GradeOfEnrollmentSpecification(Guid enrollmentId, bool tracking = false)
        {
            SetCriteria(g => g.EnrollmentId == enrollmentId);
            AddInclude("Scores");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // Every grade of a section (the grade sheet). Tracked for the whole-section steps.
    public sealed class GradesOfSectionSpecification : Specification<Grade>
    {
        public GradesOfSectionSpecification(Guid sectionId, bool tracking)
        {
            SetCriteria(g => g.CourseSectionId == sectionId);
            AddInclude("Scores");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class GradesListSpecification : Specification<Grade>
    {
        public GradesListSpecification(PaginationQuery query, Guid sectionId, GradeStatus? status)
        {
            SetCriteria(g => g.CourseSectionId == sectionId && (status == null || g.Status == status));
            AddInclude("Scores");
            ApplyOrderBy(g => g.StudentId);
            ApplyPaging(query);
        }
    }

    public sealed class GradesCountSpecification : Specification<Grade>
    {
        public GradesCountSpecification(Guid sectionId, GradeStatus? status)
            => SetCriteria(g => g.CourseSectionId == sectionId && (status == null || g.Status == status));
    }

    // The grades of one student; with finalOnly just the Published and Locked ones.
    public sealed class GradesOfStudentSpecification : Specification<Grade>
    {
        public GradesOfStudentSpecification(Guid studentId, Guid? semesterId, bool finalOnly)
        {
            SetCriteria(g => g.StudentId == studentId && (semesterId == null || g.AcademicSemesterId == semesterId)
                             && (!finalOnly || g.Status == GradeStatus.Published || g.Status == GradeStatus.Locked));
            AddInclude("Scores");
            ApplyOrderByDescending(g => g.CreatedAt);
        }
    }

    public sealed class DefaultGradeScaleSpecification : Specification<GradeScale>
    {
        public DefaultGradeScaleSpecification()
        {
            SetCriteria(s => s.IsDefault);
            AddInclude("Bands");
        }
    }

    public sealed class GradeScaleByNameSpecification : Specification<GradeScale>
    {
        public GradeScaleByNameSpecification(string name) => SetCriteria(s => s.Name == name);
    }
}
