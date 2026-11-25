using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Notifications
{
    // What the academic events need: the name of the course of a section, and the students of a section to tell. A student
    // without a login account is skipped (StudentNotifier). Delivery is at least once and the publisher ignores a second
    // notification of the same event to the same user, so a retried event does not notify twice.
    public sealed class AcademicNotifier(StudentNotifier students, IEnrollmentReadService enrollments, IOfferingReadService offerings, ICourseCatalogReadService catalog)
    {
        public const string SectionReferenceType = "CourseSection";

        private readonly StudentNotifier _students = students;
        private readonly IEnrollmentReadService _enrollments = enrollments;
        private readonly IOfferingReadService _offerings = offerings;
        private readonly ICourseCatalogReadService _catalog = catalog;

        // "CS101 Introduction to computing", or "your course" when the course can no longer be read: the notification still goes out.
        public async Task<string> CourseNameAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            var section = await _offerings.GetSectionAsync(sectionId, cancellationToken);
            if (!section.TryGetValue(out var info))
            {
                return "your course";
            }

            var version = await _catalog.GetVersionAsync(info.CourseVersionId, cancellationToken);

            return version.TryGetValue(out var course) ? $"{course.CourseCode} {course.Title}" : "your course";
        }

        public Task<Result> NotifyStudentAsync(Guid studentId, string type, string title, string content, string referenceType, Guid referenceId, Guid eventId, CancellationToken cancellationToken)
            => _students.NotifyAsync(studentId, type, title, content, referenceType, referenceId, eventId, cancellationToken);

        // Everyone on the roster of the section (registered or completed). The content is built once with the course name.
        public async Task<Result> NotifySectionAsync(Guid sectionId, string type, string title, Func<string, string> content, Guid eventId, CancellationToken cancellationToken)
        {
            var roster = await _enrollments.GetRosterAsync(sectionId, cancellationToken);
            if (!roster.TryGetValue(out var entries) || entries.Count == 0)
            {
                return roster.IsFailure ? Result.Fail(roster.Errors) : Result.Ok();
            }

            var text = content(await CourseNameAsync(sectionId, cancellationToken));

            foreach (var studentId in entries.Select(e => e.StudentId).Distinct())
            {
                var sent = await _students.NotifyAsync(studentId, type, title, text, SectionReferenceType, sectionId, eventId, cancellationToken);
                if (sent.IsFailure)
                {
                    return sent;
                }
            }

            return Result.Ok();
        }
    }
}
