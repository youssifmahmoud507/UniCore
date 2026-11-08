using UniCore.Domain.Modules.Attendance;

namespace UniCore.Application.Modules.Attendance
{
    public sealed record PolicySummary(Guid Id, Guid? AcademicProgramId, Guid? CourseId, decimal MinimumPercentage, decimal WarningPercentage, int LateThresholdMinutes, bool IsExamBlocking, bool IsActive, DateTimeOffset CreatedAt);

    public sealed record RecordSummary(Guid Id, Guid StudentId, Guid EnrollmentId, AttendanceStatus Status, int? MinutesLate, DateTimeOffset MarkedAt);

    public sealed record SessionSummary(Guid Id, Guid CourseSectionId, DateOnly SessionDate, TimeOnly StartTime, AttendanceSessionStatus Status, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, int RecordCount);

    public sealed record SessionDetail(Guid Id, Guid CourseSectionId, DateOnly SessionDate, TimeOnly StartTime, AttendanceSessionStatus Status, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, IReadOnlyList<RecordSummary> Records);

    public sealed record ExcuseSummary(Guid Id, Guid AttendanceSessionId, Guid AttendanceRecordId, Guid StudentId, Guid CourseSectionId, string Reason, Guid? DocumentId, ExcuseStatus Status, DateTimeOffset SubmittedAt, DateTimeOffset? ReviewedAt, string? ReviewNote);

    public sealed record WarningSummary(Guid Id, Guid StudentId, Guid CourseSectionId, decimal Percentage, WarningLevel Level, bool IsExamBlocking, DateTimeOffset IssuedAt);

    // One student in one section. Level = null when no warning applies (or no policy exists).
    public sealed record StudentAttendanceSummary(Guid StudentId, Guid CourseSectionId, int ClosedSessions, int Attended, int Absent, int Late, int Excused, decimal Percentage, WarningLevel? Level, bool IsExamBlocked);

    public static class AttendanceMapper
    {
        public static PolicySummary ToSummary(AttendancePolicy p)
            => new(p.Id, p.AcademicProgramId, p.CourseId, p.MinimumPercentage, p.WarningPercentage, p.LateThresholdMinutes, p.IsExamBlocking, p.IsActive, p.CreatedAt);

        public static RecordSummary ToSummary(AttendanceRecord r) => new(r.Id, r.StudentId, r.EnrollmentId, r.Status, r.MinutesLate, r.MarkedAt);

        public static SessionSummary ToSummary(AttendanceSession s)
            => new(s.Id, s.CourseSectionId, s.SessionDate, s.StartTime, s.Status, s.OpenedAt, s.ClosedAt, s.Records.Count);

        public static SessionDetail ToDetail(AttendanceSession s)
            => new(s.Id, s.CourseSectionId, s.SessionDate, s.StartTime, s.Status, s.OpenedAt, s.ClosedAt, [.. s.Records.OrderBy(r => r.MarkedAt).Select(ToSummary)]);

        public static ExcuseSummary ToSummary(AttendanceExcuse e)
            => new(e.Id, e.AttendanceSessionId, e.AttendanceRecordId, e.StudentId, e.CourseSectionId, e.Reason, e.DocumentId, e.Status, e.SubmittedAt, e.ReviewedAt, e.ReviewNote);

        public static WarningSummary ToSummary(AttendanceWarning w)
            => new(w.Id, w.StudentId, w.CourseSectionId, w.Percentage, w.Level, w.IsExamBlocking, w.IssuedAt);
    }
}
