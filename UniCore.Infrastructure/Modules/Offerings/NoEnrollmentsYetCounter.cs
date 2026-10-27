using UniCore.Application.Modules.Offerings;

namespace UniCore.Infrastructure.Modules.Offerings
{
    // Placeholder until the Registration module (Week 9) exists: nobody can be enrolled yet, so every section has 0.
    // Week 9 replaces the registration in AddOfferingsModule with a counter that reads Enrollment.
    public sealed class NoEnrollmentsYetCounter : ISectionEnrollmentCounter
    {
        public Task<int> CountEnrolledAsync(Guid courseSectionId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
