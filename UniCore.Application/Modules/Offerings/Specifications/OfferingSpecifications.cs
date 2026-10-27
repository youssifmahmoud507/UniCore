using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings.Specifications
{
    public sealed class OfferingByKeySpecification : Specification<CourseOffering>
    {
        public OfferingByKeySpecification(Guid courseVersionId, Guid semesterId)
        {
            SetCriteria(o => o.CourseVersionId == courseVersionId && o.AcademicSemesterId == semesterId);
        }
    }

    public sealed class OfferingsListSpecification : Specification<CourseOffering>
    {
        public static readonly string[] SortFields = ["createdAt", "status"];

        public OfferingsListSpecification(PaginationQuery query, Guid? semesterId, Guid? courseVersionId, CourseOfferingStatus? status, SortOption sort)
        {
            SetCriteria(o => (semesterId == null || o.AcademicSemesterId == semesterId)
                && (courseVersionId == null || o.CourseVersionId == courseVersionId)
                && (status == null || o.Status == status));

            if (sort.Field == "status")
            {
                if (sort.Descending) { ApplyOrderByDescending(o => o.Status); } else { ApplyOrderBy(o => o.Status); }
            }
            else
            {
                if (sort.Descending) { ApplyOrderByDescending(o => o.CreatedAt); } else { ApplyOrderBy(o => o.CreatedAt); }
            }

            ApplyPaging(query);
        }
    }

    // The sections of an offering by code. activeOnly leaves out the cancelled ones.
    public sealed class SectionsOfOfferingSpecification : Specification<CourseSection>
    {
        public SectionsOfOfferingSpecification(Guid offeringId, bool activeOnly, bool tracking = false)
        {
            SetCriteria(s => s.CourseOfferingId == offeringId && (!activeOnly || s.Status != CourseSectionStatus.Cancelled));
            ApplyOrderBy(s => s.SectionCode);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class SectionByCodeSpecification : Specification<CourseSection>
    {
        public SectionByCodeSpecification(Guid offeringId, string code)
        {
            SetCriteria(s => s.CourseOfferingId == offeringId && s.SectionCode == code);
        }
    }

    // The assignments of a section, oldest first. activeOnly leaves out the ended ones.
    public sealed class AssignmentsOfSectionSpecification : Specification<InstructorAssignment>
    {
        public AssignmentsOfSectionSpecification(Guid sectionId, bool activeOnly, bool tracking = false)
        {
            SetCriteria(a => a.CourseSectionId == sectionId && (!activeOnly || a.EndedAt == null));
            ApplyOrderBy(a => a.AssignedAt);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

    public sealed class ActiveAssignmentSpecification : Specification<InstructorAssignment>
    {
        public ActiveAssignmentSpecification(Guid sectionId, Guid instructorId)
        {
            SetCriteria(a => a.CourseSectionId == sectionId && a.InstructorId == instructorId && a.EndedAt == null);
        }
    }

    public sealed class ActivePrimarySpecification : Specification<InstructorAssignment>
    {
        public ActivePrimarySpecification(Guid sectionId)
        {
            SetCriteria(a => a.CourseSectionId == sectionId && a.Role == AssignmentRole.Primary && a.EndedAt == null);
        }
    }

    // One assignment, tracked, so the handler can end it.
    public sealed class AssignmentByIdSpecification : Specification<InstructorAssignment>
    {
        public AssignmentByIdSpecification(Guid assignmentId)
        {
            SetCriteria(a => a.Id == assignmentId);
            UseTracking();
        }
    }
}
