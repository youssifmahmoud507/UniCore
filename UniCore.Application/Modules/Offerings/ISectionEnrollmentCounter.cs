namespace UniCore.Application.Modules.Offerings
{
    // How many students are enrolled in a section right now (seats taken by Registered enrollments). Offerings uses it
    // to refuse cancelling a section with students and a capacity below the enrolled count. SectionSeatCounter answers
    // it from CourseSection.EnrolledCount, which Registration keeps (Week 9).
    public interface ISectionEnrollmentCounter
    {
        Task<int> CountEnrolledAsync(Guid courseSectionId, CancellationToken cancellationToken = default);
    }
}
