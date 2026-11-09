using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Examination.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.People;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Examination
{
    // Exams, their date and time, rooms and invigilators. Exams.Read to read, Exams.Manage (on the department of the
    // section's course) to change. Order of every change: permission alone -> load -> scope -> state -> change -> conflicts.
    // A conflict is found when the exam gets a time, a room or an invigilator, and again when it is published.
    public sealed class ExamHandler(
        ExaminationAccess access, ICurrentUser user, IAggregateRepository<Exam> exams, IAggregateRepository<ExamSession> sessions,
        IRepository<Room> rooms, IRepository<Instructor> instructors, ExamConflictChecker conflicts, ExamRosterService roster,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly ExaminationAccess _access = access;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Exam> _exams = exams;
        private readonly IAggregateRepository<ExamSession> _sessions = sessions;
        private readonly IRepository<Room> _rooms = rooms;
        private readonly IRepository<Instructor> _instructors = instructors;
        private readonly ExamConflictChecker _conflicts = conflicts;
        private readonly ExamRosterService _roster = roster;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private sealed record ExamContext(Exam Exam, ExamSession Session, ExaminationAccess.SectionContext Section);

        private static string Describe(Exam e)
            => $"Section={e.CourseSectionId};Session={e.ExamSessionId};Type={e.ExamType};Status={e.Status};Max={e.MaxScore};Minutes={e.DurationMinutes};"
               + $"Date={(e.Schedule is null ? "-" : e.Schedule.ExamDate.ToString("yyyy-MM-dd"))};Time={(e.Schedule is null ? "-" : e.Schedule.StartTime + "-" + e.Schedule.EndTime)};Rooms={e.Rooms.Count};Invigilators={e.Invigilators.Count}";

        private async Task<Result<ExamSummary>> CommitAsync(Exam exam, string action, string? before, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "Exam", exam.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(exam));
            var committed = await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);

            return committed.IsFailure ? Result<ExamSummary>.Fail(committed.Errors) : ExaminationMapper.ToSummary(exam);
        }

        // Manage permission -> load tracked -> section scope + open semester.
        private async Task<Result<ExamContext>> LoadForChangeAsync(Guid examId, CancellationToken cancellationToken)
        {
            var permission = await _access.RequireAsync(Permissions.Exams.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<ExamContext>.Fail(permission.Errors);
            }

            var exam = await _exams.FirstOrDefaultAsync(new ExamByIdSpecification(examId, tracking: true), cancellationToken);
            if (exam is null)
            {
                return ExaminationErrors.ExamNotFound;
            }

            var session = await _sessions.FirstOrDefaultAsync(new ExamSessionByIdSpecification(exam.ExamSessionId), cancellationToken);
            if (session is null)
            {
                return ExaminationErrors.SessionNotFound;
            }

            var section = await _access.AuthorizeSectionAsync(Permissions.Exams.Manage, exam.CourseSectionId, openSemester: true, cancellationToken);
            if (!section.TryGetValue(out var context))
            {
                return Result<ExamContext>.Fail(section.Errors);
            }

            return session.IsOpen ? new ExamContext(exam, session, context) : ExaminationErrors.SessionClosed;
        }

        // ---------- change ----------

        public async Task<Result<ExamSummary>> CreateAsync(CreateExamCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Exams.Manage, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<ExamSummary>.Fail(permission.Errors);
            }

            var session = command.ExamSessionId == Guid.Empty ? null : await _sessions.FirstOrDefaultAsync(new ExamSessionByIdSpecification(command.ExamSessionId), cancellationToken);
            if (session is null)
            {
                return ExaminationErrors.SessionNotFound;
            }

            var section = await _access.AuthorizeSectionAsync(Permissions.Exams.Manage, command.CourseSectionId, openSemester: true, cancellationToken);
            if (!section.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(section.Errors);
            }

            if (!session.IsOpen)
            {
                return ExaminationErrors.SessionClosed;
            }

            if (context.Section.AcademicSemesterId != session.AcademicSemesterId)
            {
                return ExaminationErrors.SectionSemesterMismatch;
            }

            if (command.ExamType != session.ExamType)
            {
                return ExaminationErrors.TypeMismatch;
            }

            var created = Exam.Create(command.CourseSectionId, command.ExamSessionId, command.ExamType, command.MaxScore, command.DurationMinutes, _clock.UtcNow);
            if (!created.TryGetValue(out var exam))
            {
                return Result<ExamSummary>.Fail(created.Errors);
            }

            if (await _exams.AnyAsync(new ActiveExamOfSectionSpecification(command.CourseSectionId, command.ExamType), cancellationToken))
            {
                return ExaminationErrors.ExamExists;
            }

            _exams.Add(exam);

            return await CommitAsync(exam, ExaminationAuditActions.ExamCreated, null, request, cancellationToken);
        }

        public async Task<Result<ExamSummary>> UpdateAsync(Guid examId, UpdateExamCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Exam);
            var updated = context.Exam.Update(command.MaxScore, command.DurationMinutes, _clock.UtcNow);
            if (updated.IsFailure)
            {
                return Result<ExamSummary>.Fail(updated.Errors);
            }

            _exams.Update(context.Exam);

            return await CommitAsync(context.Exam, ExaminationAuditActions.ExamUpdated, before, request, cancellationToken);
        }

        // The date must be inside the session and inside the exam window of the semester; then no conflict with other exams.
        public async Task<Result<ExamSummary>> SetScheduleAsync(Guid examId, SetExamScheduleCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var exam = context.Exam;
            var before = Describe(exam);

            var set = exam.SetSchedule(command.ExamDate, command.StartTime, command.EndTime, _clock.UtcNow);
            if (set.IsFailure)
            {
                return Result<ExamSummary>.Fail(set.Errors);
            }

            if (!context.Session.Contains(command.ExamDate))
            {
                return ExaminationErrors.DateOutsideSession;
            }

            if (ExaminationAccess.WindowOf(context.Section.Semester, exam.ExamType) is not { } window || command.ExamDate < window.Start || command.ExamDate > window.End)
            {
                return ExaminationErrors.DateOutsideWindow;
            }

            var clear = await _conflicts.CheckAsync(exam, cancellationToken);
            if (clear.IsFailure)
            {
                return Result<ExamSummary>.Fail(clear.Errors);
            }

            _exams.Update(exam);

            return await CommitAsync(exam, ExaminationAuditActions.ScheduleSet, before, request, cancellationToken);
        }

        public async Task<Result<ExamSummary>> AddRoomAsync(Guid examId, AddExamRoomCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var room = command.RoomId == Guid.Empty ? null : await _rooms.GetByIdAsync(command.RoomId, cancellationToken);
            if (room is null)
            {
                return ExaminationErrors.RoomNotFound;
            }

            if (!room.IsActive)
            {
                return ExaminationErrors.RoomInactive;
            }

            var exam = context.Exam;
            var before = Describe(exam);

            var added = exam.AddRoom(room.Id, room.Capacity, _clock.UtcNow);
            if (added.IsFailure)
            {
                return Result<ExamSummary>.Fail(added.Errors);
            }

            var clear = await _conflicts.CheckAsync(exam, cancellationToken);
            if (clear.IsFailure)
            {
                return Result<ExamSummary>.Fail(clear.Errors);
            }

            _exams.Update(exam);

            return await CommitAsync(exam, ExaminationAuditActions.RoomAdded, before, request, cancellationToken);
        }

        public async Task<Result<ExamSummary>> RemoveRoomAsync(Guid examId, Guid roomId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Exam);
            var removed = context.Exam.RemoveRoom(roomId, _clock.UtcNow);
            if (removed.IsFailure)
            {
                return Result<ExamSummary>.Fail(removed.Errors);
            }

            _exams.Update(context.Exam);

            return await CommitAsync(context.Exam, ExaminationAuditActions.RoomRemoved, before, request, cancellationToken);
        }

        public async Task<Result<ExamSummary>> AddInvigilatorAsync(Guid examId, AddInvigilatorCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var instructor = command.InstructorId == Guid.Empty ? null : await _instructors.GetByIdAsync(command.InstructorId, cancellationToken);
            if (instructor is null)
            {
                return ExaminationErrors.InvigilatorNotFound;
            }

            if (!instructor.IsActive)
            {
                return ExaminationErrors.InvigilatorInactive;
            }

            var exam = context.Exam;
            var before = Describe(exam);

            var added = exam.AddInvigilator(command.RoomId, instructor.Id, command.Role, _clock.UtcNow);
            if (added.IsFailure)
            {
                return Result<ExamSummary>.Fail(added.Errors);
            }

            var clear = await _conflicts.CheckAsync(exam, cancellationToken);
            if (clear.IsFailure)
            {
                return Result<ExamSummary>.Fail(clear.Errors);
            }

            _exams.Update(exam);

            return await CommitAsync(exam, ExaminationAuditActions.InvigilatorAdded, before, request, cancellationToken);
        }

        public async Task<Result<ExamSummary>> RemoveInvigilatorAsync(Guid examId, Guid instructorId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Exam);
            var removed = context.Exam.RemoveInvigilator(instructorId, _clock.UtcNow);
            if (removed.IsFailure)
            {
                return Result<ExamSummary>.Fail(removed.Errors);
            }

            _exams.Update(context.Exam);

            return await CommitAsync(context.Exam, ExaminationAuditActions.InvigilatorRemoved, before, request, cancellationToken);
        }

        // Needs a time, enough seats for the registered students and a chief in every room; conflicts are checked once more
        // because other exams may have been scheduled since.
        public async Task<Result<ExamSummary>> PublishAsync(Guid examId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var exam = context.Exam;
            if (!exam.IsDraft)
            {
                return ExaminationErrors.ExamInvalidState;
            }

            var registered = await _roster.CountRegisteredAsync(exam.CourseSectionId, cancellationToken);
            if (!registered.TryGetValue(out var count))
            {
                return Result<ExamSummary>.Fail(registered.Errors);
            }

            var before = Describe(exam);
            var published = exam.Publish(count, _clock.UtcNow);
            if (published.IsFailure)
            {
                return Result<ExamSummary>.Fail(published.Errors);
            }

            var clear = await _conflicts.CheckAsync(exam, cancellationToken);
            if (clear.IsFailure)
            {
                return Result<ExamSummary>.Fail(clear.Errors);
            }

            _exams.Update(exam);

            return await CommitAsync(exam, ExaminationAuditActions.ExamPublished, before, request, cancellationToken);
        }

        public async Task<Result<ExamSummary>> CancelAsync(Guid examId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<ExamSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Exam);
            var cancelled = context.Exam.Cancel(_clock.UtcNow);
            if (cancelled.IsFailure)
            {
                return Result<ExamSummary>.Fail(cancelled.Errors);
            }

            _exams.Update(context.Exam);

            return await CommitAsync(context.Exam, ExaminationAuditActions.ExamCancelled, before, request, cancellationToken);
        }

        // ---------- read ----------

        public async Task<Result<ExamSummary>> GetAsync(Guid examId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForReadAsync(examId, cancellationToken);

            return loaded.TryGetValue(out var exam) ? ExaminationMapper.ToSummary(exam) : Result<ExamSummary>.Fail(loaded.Errors);
        }

        // The exam list of the section's students, without the students blocked by attendance.
        public async Task<Result<ExamRosterSummary>> GetRosterAsync(Guid examId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForReadAsync(examId, cancellationToken);
            if (!loaded.TryGetValue(out var exam))
            {
                return Result<ExamRosterSummary>.Fail(loaded.Errors);
            }

            return await _roster.GetAsync(exam, cancellationToken);
        }

        public async Task<Result<PagedResult<ExamSummary>>> ListAsync(PaginationQuery query, Guid? sessionId, Guid? sectionId, ExamStatus? status, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireAsync(Permissions.Exams.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<PagedResult<ExamSummary>>.Fail(permission.Errors);
            }

            // A section filter is checked against the section; without it only the whole-university readers see the list.
            var scope = sectionId is { } section
                ? (await _access.AuthorizeSectionAsync(Permissions.Exams.Read, section, openSemester: false, cancellationToken)).IsFailure
                : (await _access.RequireAsync(Permissions.Exams.Manage, cancellationToken)).IsFailure;
            if (scope)
            {
                return Result<PagedResult<ExamSummary>>.Fail(ExaminationErrors.ExamNotFound);
            }

            var items = await _exams.ListAsync(new ExamsListSpecification(query, sessionId, sectionId, status), cancellationToken);
            var total = await _exams.CountAsync(new ExamsCountSpecification(sessionId, sectionId, status), cancellationToken);

            return PagedResult<ExamSummary>.Create([.. items.Select(ExaminationMapper.ToSummary)], query, total);
        }

        private async Task<Result<Exam>> LoadForReadAsync(Guid examId, CancellationToken cancellationToken)
        {
            var permission = await _access.RequireAsync(Permissions.Exams.Read, cancellationToken);
            if (permission.IsFailure)
            {
                return Result<Exam>.Fail(permission.Errors);
            }

            var exam = await _exams.FirstOrDefaultAsync(new ExamByIdSpecification(examId), cancellationToken);
            if (exam is null)
            {
                return ExaminationErrors.ExamNotFound;
            }

            var scope = await _access.AuthorizeSectionAsync(Permissions.Exams.Read, exam.CourseSectionId, openSemester: false, cancellationToken);

            return scope.IsFailure ? Result<Exam>.Fail(scope.Errors) : exam;
        }
    }
}
