namespace UniCore.Application.Modules.Reporting
{
    // One course section: where it sits (department of the course), its semester and its seats.
    public sealed record SectionEnrollmentFact(Guid DepartmentId, Guid SemesterId, string CourseCode, string CourseTitle, string SectionCode, int Capacity, int Enrolled);
}
