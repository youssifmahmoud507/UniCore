using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Registrations.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Registrations;

namespace UniCore.Application.Modules.Registrations
{
    // What other modules (grading, attendance, finance, transcript) may ask the Registrations module.
    // They never read its tables.
    public interface IEnrollmentReadService
    {
        // The enrollments of one student in one semester (every status, history included).
        Task<Result<IReadOnlyList<EnrollmentInfo>>> GetStudentEnrollmentsAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default);

        // Students on a section's roster: Registered or Completed enrollments only.
        Task<Result<IReadOnlyList<SectionRosterEntry>>> GetRosterAsync(Guid courseSectionId, CancellationToken cancellationToken = default);
    }

    public sealed record EnrollmentInfo(Guid EnrollmentId, Guid RegistrationId, Guid CourseSectionId, Guid CourseId, Guid CourseVersionId, int Credits, EnrollmentStatus Status);

    public sealed record SectionRosterEntry(Guid EnrollmentId, Guid StudentId, EnrollmentStatus Status);

    public sealed class EnrollmentReadService(IAggregateRepository<Registration> registrations) : IEnrollmentReadService
    {
        private readonly IAggregateRepository<Registration> _registrations = registrations;

        public async Task<Result<IReadOnlyList<EnrollmentInfo>>> GetStudentEnrollmentsAsync(Guid studentId, Guid semesterId, CancellationToken cancellationToken = default)
        {
            var registration = await _registrations.FirstOrDefaultAsync(new RegistrationByStudentSemesterSpecification(studentId, semesterId), cancellationToken);

            IReadOnlyList<EnrollmentInfo> items = registration is null
                ? []
                : [.. registration.Enrollments.Select(e => new EnrollmentInfo(e.Id, e.RegistrationId, e.CourseSectionId, e.CourseId, e.CourseVersionId, e.Credits, e.Status))];

            return Result<IReadOnlyList<EnrollmentInfo>>.Ok(items);
        }

        public async Task<Result<IReadOnlyList<SectionRosterEntry>>> GetRosterAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            var list = await _registrations.ListAsync(new RegistrationsWithSectionSpecification(courseSectionId), cancellationToken);

            IReadOnlyList<SectionRosterEntry> roster =
            [
                .. list.SelectMany(r => r.Enrollments
                    .Where(e => e.CourseSectionId == courseSectionId && e.Status is EnrollmentStatus.Registered or EnrollmentStatus.Completed)
                    .Select(e => new SectionRosterEntry(e.Id, r.StudentId, e.Status)))
            ];

            return Result<IReadOnlyList<SectionRosterEntry>>.Ok(roster);
        }
    }
}
