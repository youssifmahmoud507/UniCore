using System.Globalization;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Transcripts;

namespace UniCore.Application.Modules.Notifications
{
    // The academic modules raise these events; the people they concern are told here (the Developer 2 side of each event).
    // Every handler is idempotent through the event id, see NotificationPublisher.
    public sealed class AttendanceWarningNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<AttendanceWarningIssued>
    {
        public async Task<Result> HandleAsync(AttendanceWarningIssued e, CancellationToken cancellationToken = default)
        {
            var course = await notifier.CourseNameAsync(e.CourseSectionId, cancellationToken);
            var percentage = e.Percentage.ToString("0.##", CultureInfo.InvariantCulture);
            var text = e.IsExamBlocking
                ? $"Your attendance in {course} is {percentage} percent. You are below the level required to sit the exam."
                : $"Your attendance in {course} is {percentage} percent. Please attend your next sessions.";

            return await notifier.NotifyStudentAsync(e.StudentId, NotificationTypes.AttendanceWarning, e.Level == WarningLevel.Critical ? "Critical attendance warning" : "Attendance warning", text, "AttendanceWarning", e.WarningId, e.EventId, cancellationToken);
        }
    }

    public sealed class GradePublishedNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<GradePublished>
    {
        public async Task<Result> HandleAsync(GradePublished e, CancellationToken cancellationToken = default)
        {
            var course = await notifier.CourseNameAsync(e.CourseSectionId, cancellationToken);
            var percentage = e.Percentage.ToString("0.##", CultureInfo.InvariantCulture);

            return await notifier.NotifyStudentAsync(e.StudentId, NotificationTypes.GradePublished, "Grade published", $"Your grade in {course} is published: {e.Letter} ({percentage} percent).", "Grade", e.GradeId, e.EventId, cancellationToken);
        }
    }

    public sealed class GradeCorrectedNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<GradeCorrected>
    {
        public async Task<Result> HandleAsync(GradeCorrected e, CancellationToken cancellationToken = default)
        {
            var course = await notifier.CourseNameAsync(e.CourseSectionId, cancellationToken);
            var before = e.OldPercentage.ToString("0.##", CultureInfo.InvariantCulture);
            var after = e.NewPercentage.ToString("0.##", CultureInfo.InvariantCulture);

            return await notifier.NotifyStudentAsync(e.StudentId, NotificationTypes.GradeCorrected, "Grade corrected", $"Your grade in {course} was corrected from {e.OldLetter} ({before} percent) to {e.NewLetter} ({after} percent).", "Grade", e.GradeId, e.EventId, cancellationToken);
        }
    }

    public sealed class AssessmentPublishedNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<AssessmentPublished>
    {
        public Task<Result> HandleAsync(AssessmentPublished e, CancellationToken cancellationToken = default)
            => notifier.NotifySectionAsync(e.CourseSectionId, NotificationTypes.AssessmentPublished, "Grading scheme published", course => $"The grading scheme of {course} is published ({e.ComponentCount} components).", e.EventId, cancellationToken);
    }

    public sealed class AssessmentCancelledNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<AssessmentCancelled>
    {
        public Task<Result> HandleAsync(AssessmentCancelled e, CancellationToken cancellationToken = default)
            => notifier.NotifySectionAsync(e.CourseSectionId, NotificationTypes.AssessmentCancelled, "Grading scheme cancelled", course => $"The grading scheme of {course} was cancelled. A new one will be published.", e.EventId, cancellationToken);
    }

    public sealed class ExamPublishedNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<ExamPublished>
    {
        public Task<Result> HandleAsync(ExamPublished e, CancellationToken cancellationToken = default)
            => notifier.NotifySectionAsync(
                e.CourseSectionId, NotificationTypes.ExamPublished, "Exam scheduled",
                course => $"The {e.ExamType} exam of {course} is on {e.ExamDate:yyyy-MM-dd} from {e.StartTime:HH\\:mm} to {e.EndTime:HH\\:mm}.", e.EventId, cancellationToken);
    }

    public sealed class ExamCancelledNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<ExamCancelled>
    {
        public Task<Result> HandleAsync(ExamCancelled e, CancellationToken cancellationToken = default)
            => notifier.NotifySectionAsync(e.CourseSectionId, NotificationTypes.ExamCancelled, "Exam cancelled", course => $"The {e.ExamType} exam of {course} was cancelled.", e.EventId, cancellationToken);
    }

    public sealed class EnrollmentCreatedNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<EnrollmentCreated>
    {
        public async Task<Result> HandleAsync(EnrollmentCreated e, CancellationToken cancellationToken = default)
        {
            var course = await notifier.CourseNameAsync(e.CourseSectionId, cancellationToken);

            return await notifier.NotifyStudentAsync(e.StudentId, NotificationTypes.EnrollmentConfirmed, "Registration confirmed", $"You are registered in {course} ({e.Credits} credits).", "Enrollment", e.EnrollmentId, e.EventId, cancellationToken);
        }
    }

    public sealed class EnrollmentDroppedNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<EnrollmentDropped>
    {
        public async Task<Result> HandleAsync(EnrollmentDropped e, CancellationToken cancellationToken = default)
        {
            var course = await notifier.CourseNameAsync(e.CourseSectionId, cancellationToken);
            var text = e.IsWithdrawal ? $"You withdrew from {course}." : $"You are no longer registered in {course}.";

            return await notifier.NotifyStudentAsync(e.StudentId, NotificationTypes.EnrollmentDropped, e.IsWithdrawal ? "Withdrawal confirmed" : "Registration removed", text, "Enrollment", e.EnrollmentId, e.EventId, cancellationToken);
        }
    }

    public sealed class TranscriptIssuedNotificationHandler(AcademicNotifier notifier) : IDomainEventHandler<TranscriptIssued>
    {
        public Task<Result> HandleAsync(TranscriptIssued e, CancellationToken cancellationToken = default)
            => notifier.NotifyStudentAsync(e.StudentId, NotificationTypes.TranscriptIssued, "Official transcript issued", "An official transcript was issued for you. You can read it in your academic record.", "TranscriptSnapshot", e.SnapshotId, e.EventId, cancellationToken);
    }
}
