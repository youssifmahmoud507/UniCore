namespace UniCore.Application.Modules.Offerings
{
    // Contract with the Registration module (Week 9): how many students are enrolled in a section right now
    // (enrollments that still hold a seat). Offerings uses it to refuse cancelling a section with students and to
    // refuse a capacity below the enrolled count. Until Registration exists, the Infrastructure default answers 0
    // (NoEnrollmentsYetCounter); Week 9 replaces the registration in AddOfferingsModule.
    public interface ISectionEnrollmentCounter
    {
        Task<int> CountEnrolledAsync(Guid courseSectionId, CancellationToken cancellationToken = default);
    }
}
