using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Examination.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Examination
{
    // Exam sessions (the midterm or final period of a semester). Exams.Read to read, Exams.Manage to change.
    // The dates must be inside the exam window of the semester for the type.
    public sealed class ExamSessionHandler(
        ExaminationAccess access, ICurrentUser user, IAggregateRepository<ExamSession> sessions, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly ExaminationAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<ExamSession> _sessions = sessions;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(ExamSession s) => $"Semester={s.AcademicSemesterId};Name={s.Name};Type={s.ExamType};From={s.StartDate:yyyy-MM-dd};To={s.EndDate:yyyy-MM-dd};Status={s.Status}";

        private async Task<Result<ExamSessionSummary>> CommitAsync(ExamSession session, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "ExamSession", session.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(session));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<ExamSessionSummary>.Fail(committed.Errors) : ExaminationMapper.ToSummary(session);
        }

        public async Task<Result<ExamSessionSummary>> CreateAsync(CreateExamSessionCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Exams.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<ExamSessionSummary>.Fail(permission.Errors);
            }

            var semester = await _access.LoadSemesterAsync(command.AcademicSemesterId, open: true, cancellationToken);
            if (!semester.TryGetValue(out var found))
            {
                return Result<ExamSessionSummary>.Fail(semester.Errors);
            }

            var created = ExamSession.Create(command.AcademicSemesterId, command.Name, command.ExamType, command.StartDate, command.EndDate, _clock.UtcNow);
            if (!created.TryGetValue(out var session))
            {
                return Result<ExamSessionSummary>.Fail(created.Errors);
            }

            if (ExaminationAccess.WindowOf(found, session.ExamType) is not { } window || session.StartDate < window.Start || session.EndDate > window.End)
            {
                return ExaminationErrors.SessionOutsideWindow;
            }

            if (await _sessions.AnyAsync(new ExamSessionByNameSpecification(session.AcademicSemesterId, session.Name), cancellationToken))
            {
                return ExaminationErrors.SessionNameExists;
            }

            _sessions.Add(session);

            return await CommitAsync(session, ExaminationAuditActions.SessionCreated, null, request, cancellationToken);
        }

        public async Task<Result<ExamSessionSummary>> CloseAsync(Guid sessionId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Exams.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<ExamSessionSummary>.Fail(permission.Errors);
            }

            var session = await _sessions.FirstOrDefaultAsync(new ExamSessionByIdSpecification(sessionId, tracking: true), cancellationToken);
            if (session is null)
            {
                return ExaminationErrors.SessionNotFound;
            }

            var before = Describe(session);
            var closed = session.Close(_clock.UtcNow);
            if (closed.IsFailure)
            {
                return Result<ExamSessionSummary>.Fail(closed.Errors);
            }

            _sessions.Update(session);

            return await CommitAsync(session, ExaminationAuditActions.SessionClosed, before, request, cancellationToken);
        }

        public async Task<Result<PagedResult<ExamSessionSummary>>> ListAsync(PaginationQuery query, Guid? semesterId, ExamSessionStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireAsync(Permissions.Exams.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<ExamSessionSummary>>.Fail(auth.Errors);
            }

            var items = await _sessions.ListAsync(new ExamSessionsListSpecification(query, semesterId, status), cancellationToken);
            var total = await _sessions.CountAsync(new ExamSessionsCountSpecification(semesterId, status), cancellationToken);

            return PagedResult<ExamSessionSummary>.Create([.. items.Select(ExaminationMapper.ToSummary)], query, total);
        }

        public async Task<Result<ExamSessionSummary>> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            var auth = await _access.RequireAsync(Permissions.Exams.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ExamSessionSummary>.Fail(auth.Errors);
            }

            var session = await _sessions.FirstOrDefaultAsync(new ExamSessionByIdSpecification(sessionId), cancellationToken);

            return session is null ? ExaminationErrors.SessionNotFound : ExaminationMapper.ToSummary(session);
        }
    }
}
