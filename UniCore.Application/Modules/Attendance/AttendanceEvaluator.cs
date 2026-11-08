using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Attendance.Specifications;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.Registrations;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Attendance
{
    // Computes percentages and levels, and issues warnings. Reads sessions of CLOSED sessions only.
    // Policy of a student in a section: the active policy of the course, else the active policy of the student's
    // current program, else none (the percentage is still computed, but no level and no blocking).
    public sealed class AttendanceEvaluator(
        IOfferingReadService offerings, IEnrollmentReadService roster, IAggregateRepository<AttendanceSession> sessions,
        IAggregateRepository<AttendancePolicy> policies, IAggregateRepository<AttendanceWarning> warnings,
        IAggregateRepository<Student> students, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit, ICurrentUser user)
        : IAttendanceReadService, IAttendanceWarningService
    {
        private readonly IOfferingReadService _offerings = offerings;
        private readonly IEnrollmentReadService _roster = roster;
        private readonly IAggregateRepository<AttendanceSession> _sessions = sessions;
        private readonly IAggregateRepository<AttendancePolicy> _policies = policies;
        private readonly IAggregateRepository<AttendanceWarning> _warnings = warnings;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;
        private readonly ICurrentUser _user = user;

        public async Task<Result<StudentAttendanceSummary>> GetStudentAttendanceAsync(Guid studentId, Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            var section = await _offerings.GetSectionAsync(courseSectionId, cancellationToken);
            if (!section.TryGetValue(out var info))
            {
                return Result<StudentAttendanceSummary>.Fail(AttendanceErrors.SectionNotFound);
            }

            var closed = await _sessions.ListAsync(new SessionsOfSectionSpecification(courseSectionId, closedOnly: true), cancellationToken);
            var policy = await PolicyForAsync(studentId, info.CourseId, cancellationToken);

            return Summarize(studentId, courseSectionId, closed, policy);
        }

        public async Task<Result<bool>> IsBlockedFromExamAsync(Guid studentId, Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            var summary = await GetStudentAttendanceAsync(studentId, courseSectionId, cancellationToken);

            return summary.TryGetValue(out var value) ? value.IsExamBlocked : Result<bool>.Fail(summary.Errors);
        }

        // Every student on the roster of the section (for the section report).
        public async Task<Result<IReadOnlyList<StudentAttendanceSummary>>> GetSectionReportAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            var section = await _offerings.GetSectionAsync(courseSectionId, cancellationToken);
            if (!section.TryGetValue(out var info))
            {
                return Result<IReadOnlyList<StudentAttendanceSummary>>.Fail(AttendanceErrors.SectionNotFound);
            }

            var students = await _roster.GetRosterAsync(courseSectionId, cancellationToken);
            if (!students.TryGetValue(out var entries))
            {
                return Result<IReadOnlyList<StudentAttendanceSummary>>.Fail(students.Errors);
            }

            var closed = await _sessions.ListAsync(new SessionsOfSectionSpecification(courseSectionId, closedOnly: true), cancellationToken);
            var list = new List<StudentAttendanceSummary>();

            foreach (var studentId in entries.Select(e => e.StudentId).Distinct())
            {
                var policy = await PolicyForAsync(studentId, info.CourseId, cancellationToken);
                list.Add(Summarize(studentId, courseSectionId, closed, policy));
            }

            return Result<IReadOnlyList<StudentAttendanceSummary>>.Ok([.. list.OrderBy(s => s.Percentage)]);
        }

        // Issues a warning for every student whose level is worse than the level of their last warning, then saves.
        public async Task<Result<int>> EvaluateSectionAsync(Guid courseSectionId, CancellationToken cancellationToken = default)
        {
            var section = await _offerings.GetSectionAsync(courseSectionId, cancellationToken);
            if (!section.TryGetValue(out var info))
            {
                return Result<int>.Fail(AttendanceErrors.SectionNotFound);
            }

            var report = await GetSectionReportAsync(courseSectionId, cancellationToken);
            if (!report.TryGetValue(out var rows))
            {
                return Result<int>.Fail(report.Errors);
            }

            var issued = new List<AttendanceWarning>();

            foreach (var row in rows.Where(r => r.Level is not null))
            {
                // Only a level worse than every earlier warning of this student in this section issues a new one.
                var earlier = await _warnings.ListAsync(new WarningsOfStudentSpecification(row.StudentId, courseSectionId), cancellationToken);
                if (earlier.Count > 0 && earlier.Max(w => w.Level) >= row.Level)
                {
                    continue;
                }

                var policy = await PolicyForAsync(row.StudentId, info.CourseId, cancellationToken);

                var warning = AttendanceWarning.Issue(row.StudentId, courseSectionId, row.Percentage, row.Level!.Value, policy?.IsExamBlocking ?? false, _clock.UtcNow);
                if (!warning.TryGetValue(out var created))
                {
                    return Result<int>.Fail(warning.Errors);
                }

                _warnings.Add(created);
                issued.Add(created);
            }

            if (issued.Count == 0)
            {
                return Result<int>.Ok(0);
            }

            var entry = new AuditEntry(AttendanceAuditActions.WarningIssued, _user.UserId, "AttendanceWarning", courseSectionId.ToString(), null, null, null,
                $"Section={courseSectionId};Warnings={issued.Count};Critical={issued.Count(w => w.Level == WarningLevel.Critical)}");
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<int>.Fail(committed.Errors) : Result<int>.Ok(issued.Count);
        }

        // ---------- helpers ----------

        private async Task<AttendancePolicy?> PolicyForAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken)
        {
            var byCourse = await _policies.FirstOrDefaultAsync(new ActivePolicyForTargetSpecification(null, courseId), cancellationToken);
            if (byCourse is not null)
            {
                return byCourse;
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);
            if (student?.CurrentProgram is not { } current)
            {
                return null;
            }

            return await _policies.FirstOrDefaultAsync(new ActivePolicyForTargetSpecification(current.AcademicProgramId, null), cancellationToken);
        }

        public static StudentAttendanceSummary Summarize(Guid studentId, Guid courseSectionId, IEnumerable<AttendanceSession> closedSessions, AttendancePolicy? policy)
        {
            var records = closedSessions
                .Select(s => s.Records.FirstOrDefault(r => r.StudentId == studentId))
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();

            var threshold = policy?.LateThresholdMinutes ?? 0;
            var rate = AttendanceCalculator.Calculate(records.Select(r => (r.Status, r.MinutesLate)), threshold);

            WarningLevel? level = policy is null ? null : AttendanceCalculator.Evaluate(rate.Percentage, policy.MinimumPercentage, policy.WarningPercentage);
            var blocked = policy is { IsExamBlocking: true } && level == WarningLevel.Critical;

            return new StudentAttendanceSummary(
                studentId, courseSectionId, rate.ClosedSessions, rate.Attended,
                records.Count(r => r.Status == AttendanceStatus.Absent), records.Count(r => r.Status == AttendanceStatus.Late),
                records.Count(r => r.Status == AttendanceStatus.Excused), rate.Percentage, level, blocked);
        }
    }
}
