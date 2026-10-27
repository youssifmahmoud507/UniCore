using UniCore.Domain.Modules.Offerings;

namespace UniCore.Application.Modules.Offerings
{
    // Entity -> DTO. The entities are never returned to the API directly.
    public static class OfferingMapper
    {
        public static OfferingSummary ToSummary(CourseOffering o)
            => new(o.Id, o.CourseVersionId, o.AcademicSemesterId, o.Status, o.MaxSections, o.CreatedAt, o.UpdatedAt);

        public static SectionSummary ToSummary(CourseSection s)
            => new(s.Id, s.CourseOfferingId, s.SectionCode, s.Capacity, s.Status, s.CreatedAt, s.UpdatedAt);

        public static AssignmentSummary ToSummary(InstructorAssignment a)
            => new(a.Id, a.CourseSectionId, a.InstructorId, a.Role, a.AssignedAt, a.EndedAt);
    }
}
