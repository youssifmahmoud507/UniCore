using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Attendance.Specifications;
using UniCore.Application.Modules.Documents;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Attendance
{
    // Excuses for absences. A student submits for their OWN absence (Attendance.SubmitExcuse); the supporting file is
    // stored by IDocumentService (type ATTENDANCE_EXCUSE) and only its id is kept. A reviewer (Attendance.ReviewExcuse,
    // on the section's department) accepts (the absence becomes Excused) or rejects (a note is required).
    public sealed class AttendanceExcuseHandler(
        AttendanceAccess access, ICurrentUser user, IAggregateRepository<AttendanceSession> sessions, IAggregateRepository<AttendanceExcuse> excuses,
        IDocumentService documents, IAttendanceWarningService warnings, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly AttendanceAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<AttendanceSession> _sessions = sessions;
        private readonly IAggregateRepository<AttendanceExcuse> _excuses = excuses;
        private readonly IDocumentService _documents = documents;
        private readonly IAttendanceWarningService _warnings = warnings;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(AttendanceExcuse e)
            => $"Student={e.StudentId};Section={e.CourseSectionId};Record={e.AttendanceRecordId};Status={e.Status};Document={e.DocumentId}";

        private async Task<Result<ExcuseSummary>> CommitAsync(AttendanceExcuse excuse, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "AttendanceExcuse", excuse.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(excuse));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<ExcuseSummary>.Fail(committed.Errors) : AttendanceMapper.ToSummary(excuse);
        }

        public async Task<Result<ExcuseSummary>> SubmitAsync(SubmitExcuseCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.SubmitExcuse, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<ExcuseSummary>.Fail(permission.Errors);
            }

            var session = await _sessions.FirstOrDefaultAsync(new SessionByRecordIdSpecification(command.AttendanceRecordId), cancellationToken);
            var record = session?.FindRecord(command.AttendanceRecordId);
            if (session is null || record is null)
            {
                return AttendanceErrors.RecordNotFound;
            }

            // Own absence only (ownership of the student account).
            var student = await _access.AuthorizeStudentAsync(Permissions.Attendance.SubmitExcuse, record.StudentId, cancellationToken);
            if (student.IsFailure)
            {
                return Result<ExcuseSummary>.Fail(student.Errors);
            }

            var semester = await _access.LoadSectionAsync(session.CourseSectionId, cancellationToken);
            if (semester.TryGetValue(out var context) && context.Semester.IsClosed)
            {
                return AttendanceErrors.SemesterClosed;
            }

            if (record.Status != AttendanceStatus.Absent)
            {
                return AttendanceErrors.ExcuseNotAbsence;
            }

            if (await _excuses.AnyAsync(new ActiveExcuseOfRecordSpecification(record.Id), cancellationToken))
            {
                return AttendanceErrors.ExcuseExists;
            }

            // Validate everything before storing the file, so a bad request leaves no orphan document.
            var check = AttendanceExcuse.Submit(session.Id, record.Id, record.StudentId, session.CourseSectionId, command.Reason, null, _clock.UtcNow);
            if (check.IsFailure)
            {
                return Result<ExcuseSummary>.Fail(check.Errors);
            }

            Guid? documentId = null;
            if (command.File is not null)
            {
                var uploaded = await _documents.UploadAsync(DocumentTypeCodes.AttendanceExcuse, command.File, _user.UserId, _user.UserId, cancellationToken);
                if (!uploaded.TryGetValue(out var document))
                {
                    return Result<ExcuseSummary>.Fail(uploaded.Errors);
                }

                var linkable = await _documents.VerifyLinkableAsync(document.Id, cancellationToken);
                if (linkable.IsFailure)
                {
                    return Result<ExcuseSummary>.Fail(linkable.Errors);
                }

                documentId = document.Id;
            }

            var created = AttendanceExcuse.Submit(session.Id, record.Id, record.StudentId, session.CourseSectionId, command.Reason, documentId, _clock.UtcNow);
            if (!created.TryGetValue(out var excuse))
            {
                return Result<ExcuseSummary>.Fail(created.Errors);
            }

            _excuses.Add(excuse);

            return await CommitAsync(excuse, AttendanceAuditActions.ExcuseSubmitted, null, request, cancellationToken);
        }

        public async Task<Result<ExcuseSummary>> ReviewAsync(Guid excuseId, ReviewExcuseCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Attendance.ReviewExcuse, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<ExcuseSummary>.Fail(permission.Errors);
            }

            var excuse = await _excuses.FirstOrDefaultAsync(new ExcuseByIdSpecification(excuseId, tracking: true), cancellationToken);
            if (excuse is null)
            {
                return AttendanceErrors.ExcuseNotFound;
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Attendance.ReviewExcuse, excuse.CourseSectionId, openSemester: true, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<ExcuseSummary>.Fail(scope.Errors);
            }

            if (excuse.Status != ExcuseStatus.Pending)
            {
                return AttendanceErrors.ExcuseNotPending;
            }

            var before = Describe(excuse);
            var reviewer = _user.UserId ?? Guid.Empty;

            if (command.Accept)
            {
                var session = await _sessions.FirstOrDefaultAsync(new SessionByRecordIdSpecification(excuse.AttendanceRecordId, tracking: true), cancellationToken);
                if (session is null)
                {
                    return AttendanceErrors.RecordNotFound;
                }

                var excused = session.ExcuseRecord(excuse.AttendanceRecordId);
                if (excused.IsFailure)
                {
                    return Result<ExcuseSummary>.Fail(excused.Errors);
                }

                var accepted = excuse.Accept(reviewer, command.Note, _clock.UtcNow);
                if (accepted.IsFailure)
                {
                    return Result<ExcuseSummary>.Fail(accepted.Errors);
                }

                _sessions.Update(session);
            }
            else
            {
                var rejected = excuse.Reject(reviewer, command.Note, _clock.UtcNow);
                if (rejected.IsFailure)
                {
                    return Result<ExcuseSummary>.Fail(rejected.Errors);
                }
            }

            _excuses.Update(excuse);

            var result = await CommitAsync(excuse, command.Accept ? AttendanceAuditActions.ExcuseAccepted : AttendanceAuditActions.ExcuseRejected, before, request, cancellationToken);

            // An accepted excuse raises the percentage; recompute (only worse levels issue warnings, so this never withdraws one).
            if (result.IsSuccess && command.Accept)
            {
                _ = await _warnings.EvaluateSectionAsync(excuse.CourseSectionId, cancellationToken);
            }

            return result;
        }

        public async Task<Result<PagedResult<ExcuseSummary>>> ListAsync(Guid? sectionId, Guid? studentId, ExcuseStatus? status, PaginationQuery query, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireAsync(Permissions.Attendance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<ExcuseSummary>>.Fail(auth.Errors);
            }

            if (sectionId is null && studentId is null)
            {
                return AttendanceErrors.FilterRequired;
            }

            if (sectionId is { } section)
            {
                var scope = await _access.AuthorizeSectionAsync(Permissions.Attendance.Read, section, openSemester: false, cancellationToken);
                if (scope.IsFailure)
                {
                    return Result<PagedResult<ExcuseSummary>>.Fail(scope.Errors);
                }
            }
            else
            {
                var scope = await _access.AuthorizeStudentAsync(Permissions.Attendance.Read, studentId!.Value, cancellationToken);
                if (scope.IsFailure)
                {
                    return Result<PagedResult<ExcuseSummary>>.Fail(scope.Errors);
                }
            }

            var items = await _excuses.ListAsync(new ExcusesListSpecification(query, sectionId, studentId, status), cancellationToken);
            var total = await _excuses.CountAsync(new ExcusesCountSpecification(sectionId, studentId, status), cancellationToken);

            return PagedResult<ExcuseSummary>.Create([.. items.Select(AttendanceMapper.ToSummary)], query, total);
        }
    }
}
