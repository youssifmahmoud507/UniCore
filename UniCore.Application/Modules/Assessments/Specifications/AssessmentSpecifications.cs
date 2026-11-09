using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Assessments;

namespace UniCore.Application.Modules.Assessments.Specifications
{
    public sealed class AssessmentByIdSpecification : Specification<Assessment>
    {
        public AssessmentByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(a => a.Id == id);
            AddInclude("Components");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    // The live scheme of a section: anything but Cancelled.
    public sealed class LiveAssessmentOfSectionSpecification : Specification<Assessment>
    {
        public LiveAssessmentOfSectionSpecification(Guid sectionId, bool withComponents = false)
        {
            SetCriteria(a => a.CourseSectionId == sectionId && a.Status != AssessmentStatus.Cancelled);

            if (withComponents)
            {
                AddInclude("Components");
            }
        }
    }

    public sealed class AssessmentsListSpecification : Specification<Assessment>
    {
        public AssessmentsListSpecification(PaginationQuery query, Guid? sectionId, AssessmentStatus? status)
        {
            SetCriteria(a => (sectionId == null || a.CourseSectionId == sectionId) && (status == null || a.Status == status));
            AddInclude("Components");
            ApplyOrderByDescending(a => a.CreatedAt);
            ApplyPaging(query);
        }
    }

    public sealed class AssessmentsCountSpecification : Specification<Assessment>
    {
        public AssessmentsCountSpecification(Guid? sectionId, AssessmentStatus? status)
            => SetCriteria(a => (sectionId == null || a.CourseSectionId == sectionId) && (status == null || a.Status == status));
    }
}
