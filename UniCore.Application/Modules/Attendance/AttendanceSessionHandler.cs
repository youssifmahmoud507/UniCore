using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Attendance.Specifications;
using UniCore.Application.Modules.Registrations;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Attendance
{
    // Sessions and marking. Attendance.Record, held on the section (an instructor assigned to it), opens, marks and
    // closes a session. Attendance.Read reads it. Nothing changes in a closed semester.
    // Order of every change: permission alone -> load -> scope (section) -> state -> change.
    public sealed class AttendanceSessionHandler(
        AttendanceAccess access, ICurrentUser user, IAggregateRepository<AttendanceSession> sessions, IEnrollmentReadService roster,
        IAttendanceWarningService warnings, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly AttendanceAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<AttendanceSession> _sessions = sessions;
        private readonly IEnrollmentReadService _roster = roster;
        private readonly IAttendanceWarningService _warnings = warnings;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(AttendanceSession s)
            => $"Section={s.CourseSectionId};Date={s.SessionDate:yyyy-MM-dd};Start={s.StartTime};Status={s.Status};Records={s.Records.Count}";

        private async Task<Result> CommitAsync(AttendanceSession session, string action, string? before, RequestInfo request, CancellationToken cancellationToken, string? extra = null)
        {
            var entry = new AuditEntry(action, _user.UserId, "AttendanceSession", session.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(session) + extra);

            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }

        // ---------- change ----------

        public async Task<Result<SessionSummary>> OpenAsync(OpenSessionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.Record, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<SessionSummary>.Fail(permission.Errors);
            }

            var authorized = await _access.AuthorizeSectionAsync(Permissions.Attendance.Record, command.CourseSectionId, openSemester: true, cancellationToken);
            if (!authorized.TryGetValue(out var context))
            {
                return Result<SessionSummary>.Fail(authorized.Errors);
            }

            if (command.SessionDate < context.Semester.StartDate || command.SessionDate > context.Semester.EndDate)
            {
                return AttendanceErrors.SessionDateOutsideSemester;
            }

            var created = AttendanceSession.Open(command.CourseSectionId, command.SessionDate, command.StartTime, _user.UserId, _clock.UtcNow);
            if (!created.TryGetValue(out var session))
            {
                return Result<SessionSummary>.Fail(created.Errors);
            }

            if (await _sessions.AnyAsync(new SessionBySlotSpecification(command.CourseSectionId, command.SessionDate, command.StartTime), cancellationToken))
            {
                return AttendanceErrors.SessionExists;
            }

            _sessions.Add(session);

            var committed = await CommitAsync(session, AttendanceAuditActions.SessionOpened, null, request, cancellationToken);

            return committed.IsFailure ? Result<SessionSummary>.Fail(committed.Errors) : AttendanceMapper.ToSummary(session);
        }

        // Marks (or re-marks) students while the session is open. All or nothing: the first invalid mark fails the call.
        public async Task<Result<SessionDetail>> MarkAsync(Guid sessionId, MarkAttendanceCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(sessionId, cancellationToken);
            if (!loaded.TryGetValue(out var session))
            {
                return Result<SessionDetail>.Fail(loaded.Errors);
            }

            if (!session.IsOpen)
            {
                return AttendanceErrors.SessionClosed;
            }

            if (command.Marks is null || command.Marks.Count == 0 || command.Marks.Select(m => m.StudentId).Distinct().Count() != command.Marks.Count)
            {
                return AttendanceErrors.MarksRequired;
            }

            var students = await _roster.GetRosterAsync(session.CourseSectionId, cancellationToken);
            if (!students.TryGetValue(out var entries))
            {
                return Result<SessionDetail>.Fail(students.Errors);
            }

            var before = Describe(session);

            foreach (var mark in command.Marks)
            {
                var entry = entries.FirstOrDefault(e => e.StudentId == mark.StudentId);
                if (entry is null)
                {
                    return AttendanceErrors.StudentNotOnRoster;
                }

                var marked = session.Mark(mark.StudentId, entry.EnrollmentId, mark.Status, mark.MinutesLate, _user.UserId, _clock.UtcNow);
                if (marked.IsFailure)
                {
                    return Result<SessionDetail>.Fail(marked.Errors);
                }
            }

            _sessions.Update(session);

            var committed = await CommitAsync(session, AttendanceAuditActions.AttendanceMarked, before, request, cancellationToken, $";Marked={command.Marks.Count}");

            return committed.IsFailure ? Result<SessionDetail>.Fail(committed.Errors) : AttendanceMapper.ToDetail(session);
        }

        // Students nobody marked become Absent, the session closes, then the warnings of the section are recomputed.
        public async Task<Result<SessionDetail>> CloseAsync(Guid sessionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(sessionId, cancellationToken);
            if (!loaded.TryGetValue(out var session))
            {
                return Result<SessionDetail>.Fail(loaded.Errors);
            }

            if (!session.IsOpen)
            {
                return AttendanceErrors.SessionNotOpen;
            }

            var students = await _roster.GetRosterAsync(session.CourseSectionId, cancellationToken);
            if (!students.TryGetValue(out var entries))
            {
                return Result<SessionDetail>.Fail(students.Errors);
            }

            var before = Describe(session);
            var autoAbsent = 0;

            foreach (var entry in entries.Where(e => session.Records.All(r => r.StudentId != e.StudentId)))
            {
                var marked = session.Mark(entry.StudentId, entry.EnrollmentId, AttendanceStatus.Absent, null, _user.UserId, _clock.UtcNow);
                if (marked.IsFailure)
                {
                    return Result<SessionDetail>.Fail(marked.Errors);
                }

                autoAbsent++;
            }

            var closed = session.Close(_clock.UtcNow);
            if (closed.IsFailure)
            {
                return Result<SessionDetail>.Fail(closed.Errors);
            }

            _sessions.Update(session);

            var committed = await CommitAsync(session, AttendanceAuditActions.SessionClosed, before, request, cancellationToken, $";AutoAbsent={autoAbsent}");
            if (committed.IsFailure)
            {
                return Result<SessionDetail>.Fail(committed.Errors);
            }

            // The close is already saved. A failure here is not the caller's failure: the warnings are recomputed on the next close or by the warnings job.
            _ = await _warnings.EvaluateSectionAsync(session.CourseSectionId, cancellationToken);

            return AttendanceMapper.ToDetail(session);
        }

        // ---------- read ----------

        public async Task<Result<PagedResult<SessionSummary>>> ListAsync(Guid sectionId, AttendanceSessionStatus? status, PaginationQuery query, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireAsync(Permissions.Attendance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<SessionSummary>>.Fail(auth.Errors);
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Attendance.Read, sectionId, openSemester: false, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<PagedResult<SessionSummary>>.Fail(scope.Errors);
            }

            var items = await _sessions.ListAsync(new SessionsListSpecification(query, sectionId, status), cancellationToken);
            var total = await _sessions.CountAsync(new SessionsCountSpecification(sectionId, status), cancellationToken);

            return PagedResult<SessionSummary>.Create([.. items.Select(AttendanceMapper.ToSummary)], query, total);
        }

        public async Task<Result<SessionDetail>> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireAsync(Permissions.Attendance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<SessionDetail>.Fail(auth.Errors);
            }

            var session = await _sessions.FirstOrDefaultAsync(new SessionByIdSpecification(sessionId), cancellationToken);
            if (session is null)
            {
                return AttendanceErrors.SessionNotFound;
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Attendance.Read, session.CourseSectionId, openSemester: false, cancellationToken);

            return scope.IsFailure ? Result<SessionDetail>.Fail(scope.Errors) : AttendanceMapper.ToDetail(session);
        }

        // Record permission -> load tracked -> section scope + open semester.
        private async Task<Result<AttendanceSession>> LoadForChangeAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.Record, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<AttendanceSession>.Fail(permission.Errors);
            }

            var session = await _sessions.FirstOrDefaultAsync(new SessionByIdSpecification(sessionId, tracking: true), cancellationToken);
            if (session is null)
            {
                return AttendanceErrors.SessionNotFound;
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Attendance.Record, session.CourseSectionId, openSemester: true, cancellationToken);

            return scope.IsFailure ? Result<AttendanceSession>.Fail(scope.Errors) : session;
        }
    }
}
