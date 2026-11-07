using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Handlers;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Common.Sorting;
using UniCore.Application.Modules.Calendar;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Registrations.Specifications;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Registrations
{
    // Register, submit, cancel, drop, withdraw and read registrations.
    //
    // Check order in every change: permission alone -> load -> scope (student's own account or the program's
    // department) -> state / window -> the change. A student may act on their own registration only; registration staff
    // (Registration.Manage) act inside their scope.
    //
    // Seats: taking and releasing a seat changes CourseSection.EnrolledCount, saved in the SAME SaveChanges as the
    // enrollment. CourseSection has a RowVersion, so when N requests race for the last seat only one save succeeds; the
    // others get CONCURRENCY_CONFLICT (retry with the same Idempotency-Key) and then COURSE_FULL.
    public sealed class RegistrationHandler(
        IPermissionAuthorizer authorizer, ICurrentUser user, RegistrationAccess access, RegistrationPipeline pipeline,
        IAggregateRepository<Registration> registrations, IAggregateRepository<CourseSection> sections,
        IAggregateRepository<Student> students, IRepository<AcademicSemester> semesters,
        IAcademicCalendarService calendar, IPrerequisiteEvaluator prerequisites,
        IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly RegistrationAccess _access = access;
        private readonly RegistrationPipeline _pipeline = pipeline;
        private readonly IAggregateRepository<Registration> _registrations = registrations;
        private readonly IAggregateRepository<CourseSection> _sections = sections;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly IAcademicCalendarService _calendar = calendar;
        private readonly IPrerequisiteEvaluator _prerequisites = prerequisites;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private static string Describe(Registration r) => $"Student={r.StudentId};Semester={r.AcademicSemesterId};Status={r.Status};Credits={r.RegisteredCredits}";

        private async Task<Result> CommitAsync(Registration registration, string action, string? before, string extra, RequestInfo request, CancellationToken cancellationToken)
        {
            var entry = new AuditEntry(action, _user.UserId, "Registration", registration.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(registration) + extra);
            return await ChangeSupport.CommitAsync(_unitOfWork, _audit, entry, cancellationToken);
        }

        // ---------- queries ----------

        public async Task<Result<RegistrationSummary>> GetAsync(Guid registrationId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireReadPermissionAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(permission.Errors);
            }

            var registration = await _registrations.FirstOrDefaultAsync(new RegistrationByIdSpecification(registrationId), cancellationToken);
            if (registration is null)
            {
                return RegistrationsErrors.RegistrationNotFound;
            }

            var scope = await _access.AuthorizeAsync(registration.StudentId, write: false, cancellationToken);

            return scope.IsFailure ? Result<RegistrationSummary>.Fail(scope.Errors) : RegistrationMapper.ToSummary(registration);
        }

        // The signed-in student's registration of a semester.
        public async Task<Result<RegistrationSummary>> GetMineAsync(Guid semesterId, CancellationToken cancellationToken = default)
        {
            var permission = await _access.RequireReadPermissionAsync(cancellationToken);
            if (permission.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(permission.Errors);
            }

            if (_user.UserId is not { } userId)
            {
                return RegistrationsErrors.StudentNotFound;
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByUserSpecification(userId), cancellationToken);
            if (student is null)
            {
                return RegistrationsErrors.StudentNotFound;
            }

            var registration = await _registrations.FirstOrDefaultAsync(new RegistrationByStudentSemesterSpecification(student.Id, semesterId), cancellationToken);

            return registration is null ? RegistrationsErrors.RegistrationNotFound : RegistrationMapper.ToSummary(registration);
        }

        public async Task<Result<PagedResult<RegistrationSummary>>> ListAsync(
            Guid studentId, Guid? semesterId, RegistrationStatus? status, string? sort, PaginationQuery query, CancellationToken cancellationToken = default)
        {
            var scope = await _access.AuthorizeAsync(studentId, write: false, cancellationToken);
            if (scope.IsFailure)
            {
                return Result<PagedResult<RegistrationSummary>>.Fail(scope.Errors);
            }

            var parsed = ListQueryRules.ParseSort(sort, RegistrationsOfStudentSpecification.SortFields, "createdAt");
            if (!parsed.TryGetValue(out var option))
            {
                return Result<PagedResult<RegistrationSummary>>.Fail(parsed.Errors);
            }

            var items = await _registrations.ListAsync(new RegistrationsOfStudentSpecification(query, studentId, semesterId, status, option), cancellationToken);
            var total = await _registrations.CountAsync(new RegistrationsOfStudentCountSpecification(studentId, semesterId, status), cancellationToken);

            return PagedResult<RegistrationSummary>.Create([.. items.Select(r => RegistrationMapper.ToSummary(r))], query, total);
        }

        // ---------- register ----------

        public async Task<Result<RegisterOutcome>> RegisterAsync(RegisterCommand command, string? idempotencyKey, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (command.StudentId == Guid.Empty || command.AcademicSemesterId == Guid.Empty || command.CourseSectionId == Guid.Empty)
            {
                return RegistrationsErrors.RequestInvalid;
            }

            var loaded = await _access.AuthorizeAsync(command.StudentId, write: true, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<RegisterOutcome>.Fail(loaded.Errors);
            }

            var key = Registration.NormalizeKey(idempotencyKey);
            if (key is null)
            {
                return RegistrationsErrors.IdempotencyKeyInvalid;
            }

            var overrideReason = command.OverrideReason?.Trim();
            var isOverride = !string.IsNullOrEmpty(overrideReason);

            if (isOverride)
            {
                if (!context.IsStaff)
                {
                    return RegistrationsErrors.OverrideForbidden;
                }

                if (overrideReason!.Length is < 5 or > 500)
                {
                    return RegistrationsErrors.OverrideReasonRequired;
                }
            }

            var semester = await _semesters.GetByIdAsync(command.AcademicSemesterId, cancellationToken);
            if (semester is null)
            {
                return RegistrationsErrors.SemesterNotFound;
            }

            var registration = await _registrations.FirstOrDefaultAsync(new RegistrationByStudentSemesterSpecification(command.StudentId, command.AcademicSemesterId, tracking: true), cancellationToken);

            // Replay first: the same key answers the same way, whatever changed since.
            if (registration?.FindByKey(key) is { } previous)
            {
                return previous.CourseSectionId == command.CourseSectionId
                    ? Replay(registration, previous)
                    : RegistrationsErrors.IdempotencyKeyReused;
            }

            if (registration is not null && !registration.AcceptsChanges)
            {
                return RegistrationsErrors.InvalidState;
            }

            // A course the student withdrew from (or completed) cannot be taken again in this registration.
            if (registration?.Enrollments.FirstOrDefault(e => e.CourseSectionId == command.CourseSectionId) is { } held
                && (held.Status == EnrollmentStatus.Withdrawn || held.Status == EnrollmentStatus.Completed))
            {
                return RegistrationsErrors.EnrollmentInvalidState;
            }

            // The ordered checks. Nothing has changed so far.
            var checkedSection = await _pipeline.CheckAsync(new PipelineInput(context.Student, semester, command.CourseSectionId, registration, SkipRegistrationWindow: isOverride), cancellationToken);
            if (!checkedSection.TryGetValue(out var info))
            {
                return Result<RegisterOutcome>.Fail(checkedSection.Errors);
            }

            // The last step: the seat.
            var section = await _sections.GetByIdAsync(info.SectionId, cancellationToken);
            if (section is null)
            {
                return RegistrationsErrors.SectionNotFound;
            }

            var now = _clock.UtcNow;

            var seat = section.TakeSeat(now);
            if (seat.IsFailure)
            {
                return Result<RegisterOutcome>.Fail(seat.Errors);
            }

            var before = registration is null ? null : Describe(registration);

            if (registration is null)
            {
                var created = Registration.Create(command.StudentId, command.AcademicSemesterId, now);
                if (!created.TryGetValue(out registration))
                {
                    return Result<RegisterOutcome>.Fail(created.Errors);
                }

                _registrations.Add(registration);
            }

            var enrolled = registration.Enroll(info.SectionId, info.CourseId, info.CourseVersionId, info.Credits, key, now);
            if (!enrolled.TryGetValue(out var enrollment))
            {
                return Result<RegisterOutcome>.Fail(enrolled.Errors);
            }

            _sections.Update(section);

            var extra = $";Enrollment={enrollment.Id};Section={info.SectionId};Course={info.CourseId}" + (isOverride ? $";OverrideReason={overrideReason}" : string.Empty);
            var action = isOverride ? RegistrationsAuditActions.RegistrarOverride : RegistrationsAuditActions.EnrollmentRegistered;

            var entry = new AuditEntry(action, _user.UserId, "Registration", registration.Id.ToString(), request.IpAddress, request.UserAgent, before, Describe(registration) + extra);
            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (saved.IsFailure)
            {
                if (saved.Error?.Code == CommonErrors.DuplicateResource.Code)
                {
                    // A parallel request created this student's registration (or used the key) first.
                    var winner = await _registrations.FirstOrDefaultAsync(new RegistrationByStudentSemesterSpecification(command.StudentId, command.AcademicSemesterId), cancellationToken);
                    if (winner?.FindByKey(key) is { } same && same.CourseSectionId == command.CourseSectionId)
                    {
                        return Replay(winner, same);
                    }

                    return CommonErrors.ConcurrencyConflict;
                }

                // CONCURRENCY_CONFLICT lands here: another request took a seat of the section first. The client may retry
                // with the same key and will then get COURSE_FULL if the seat is gone.
                return Result<RegisterOutcome>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(entry, cancellationToken);

            return new RegisterOutcome(RegistrationMapper.ToSummary(registration), RegistrationMapper.ToSummary(enrollment), IsReplay: false);
        }

        private static RegisterOutcome Replay(Registration registration, Enrollment enrollment)
            => new(RegistrationMapper.ToSummary(registration), RegistrationMapper.ToSummary(enrollment), IsReplay: true);

        // ---------- submit / cancel / complete ----------

        public async Task<Result<RegistrationSummary>> SubmitAsync(Guid registrationId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(registrationId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<RegistrationSummary>.Fail(loaded.Errors);
            }

            var before = Describe(context.Registration);

            var submitted = context.Registration.Submit(_clock.UtcNow);
            if (submitted.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(submitted.Errors);
            }

            _registrations.Update(context.Registration);

            var committed = await CommitAsync(context.Registration, RegistrationsAuditActions.RegistrationSubmitted, before, string.Empty, request, cancellationToken);

            return committed.IsFailure ? Result<RegistrationSummary>.Fail(committed.Errors) : RegistrationMapper.ToSummary(context.Registration);
        }

        // A student may cancel only while registration is open; registration staff at any time (inside their scope).
        public async Task<Result<RegistrationSummary>> CancelAsync(Guid registrationId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(registrationId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<RegistrationSummary>.Fail(loaded.Errors);
            }

            var registration = context.Registration;

            if (!registration.AcceptsChanges)
            {
                return RegistrationsErrors.InvalidState;
            }

            if (!context.IsStaff)
            {
                var window = await RequireWindowAsync(registration.AcademicSemesterId, registrationWindowOnly: true, cancellationToken);
                if (window.IsFailure)
                {
                    return Result<RegistrationSummary>.Fail(window.Errors);
                }
            }

            var before = Describe(registration);

            var cancelled = registration.Cancel(_clock.UtcNow);
            if (!cancelled.TryGetValue(out var dropped))
            {
                return Result<RegistrationSummary>.Fail(cancelled.Errors);
            }

            var released = await ReleaseSeatsAsync(dropped, cancellationToken);
            if (released.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(released.Errors);
            }

            _registrations.Update(registration);

            var committed = await CommitAsync(registration, RegistrationsAuditActions.RegistrationCancelled, before, $";Released={dropped.Count}", request, cancellationToken);

            return committed.IsFailure ? Result<RegistrationSummary>.Fail(committed.Errors) : RegistrationMapper.ToSummary(registration);
        }

        // Registration staff close one registration by hand (the semester-close job does it for all of them).
        public async Task<Result<RegistrationSummary>> CompleteAsync(Guid registrationId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadForChangeAsync(registrationId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<RegistrationSummary>.Fail(loaded.Errors);
            }

            if (!context.IsStaff)
            {
                return Result<RegistrationSummary>.Fail(Error.Forbidden("REGISTRATION_COMPLETE_FORBIDDEN", "Only registration staff can complete a registration."));
            }

            var before = Describe(context.Registration);

            var completed = context.Registration.Complete(_clock.UtcNow);
            if (completed.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(completed.Errors);
            }

            _registrations.Update(context.Registration);

            var committed = await CommitAsync(context.Registration, RegistrationsAuditActions.RegistrationCompleted, before, string.Empty, request, cancellationToken);

            return committed.IsFailure ? Result<RegistrationSummary>.Fail(committed.Errors) : RegistrationMapper.ToSummary(context.Registration);
        }

        // ---------- drop / withdraw ----------

        // Add/drop: allowed while registration or the add/drop period is open. Enrollments already dropped are skipped
        // (the call is safe to repeat). A course another enrolled course needs as corequisite cannot be dropped alone.
        public async Task<Result<RegistrationSummary>> DropAsync(Guid registrationId, DropCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            if (command.EnrollmentIds is null || command.EnrollmentIds.Count == 0 || command.EnrollmentIds.Any(id => id == Guid.Empty))
            {
                return RegistrationsErrors.EnrollmentsRequired;
            }

            return await LeaveAsync(registrationId, [.. command.EnrollmentIds.Distinct()], withdraw: false, request, cancellationToken);
        }

        // Withdrawal: only after add/drop ended and until the withdrawal deadline.
        public Task<Result<RegistrationSummary>> WithdrawAsync(Guid registrationId, Guid enrollmentId, RequestInfo request, CancellationToken cancellationToken = default)
            => enrollmentId == Guid.Empty
                ? Task.FromResult<Result<RegistrationSummary>>(RegistrationsErrors.EnrollmentsRequired)
                : LeaveAsync(registrationId, [enrollmentId], withdraw: true, request, cancellationToken);

        private async Task<Result<RegistrationSummary>> LeaveAsync(Guid registrationId, IReadOnlyList<Guid> enrollmentIds, bool withdraw, RequestInfo request, CancellationToken cancellationToken)
        {
            var loaded = await LoadForChangeAsync(registrationId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return Result<RegistrationSummary>.Fail(loaded.Errors);
            }

            var registration = context.Registration;

            if (!registration.AcceptsChanges)
            {
                return RegistrationsErrors.InvalidState;
            }

            // Only the registration's own enrollments can be addressed.
            var targets = new List<Enrollment>();
            foreach (var id in enrollmentIds)
            {
                var enrollment = registration.Enrollments.FirstOrDefault(e => e.Id == id);
                if (enrollment is null)
                {
                    return RegistrationsErrors.EnrollmentNotFound;
                }

                targets.Add(enrollment);
            }

            // Dropping again is a no-op; withdrawing an already-dropped course is an invalid state (the domain says so).
            if (!withdraw)
            {
                targets = [.. targets.Where(e => e.Status != EnrollmentStatus.Dropped)];
                if (targets.Count == 0)
                {
                    return RegistrationMapper.ToSummary(registration);
                }
            }

            var window = await RequireWindowAsync(registration.AcademicSemesterId, registrationWindowOnly: false, cancellationToken, withdraw);
            if (window.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(window.Errors);
            }

            if (context.Student.AcademicStatus != StudentAcademicStatus.Active)
            {
                return RegistrationsErrors.StudentInactive;
            }

            var blocked = await CheckCorequisiteDropAsync(registration, targets, cancellationToken);
            if (blocked.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(blocked.Errors);
            }

            var before = Describe(registration);
            var now = _clock.UtcNow;
            var left = new List<Enrollment>();

            foreach (var target in targets)
            {
                var result = withdraw ? registration.Withdraw(target.Id, now) : registration.Drop(target.Id, now);
                if (!result.TryGetValue(out var enrollment))
                {
                    // Nothing was saved, and this request is dropped with its in-memory changes.
                    return Result<RegistrationSummary>.Fail(result.Errors);
                }

                left.Add(enrollment);
            }

            var released = await ReleaseSeatsAsync(left, cancellationToken);
            if (released.IsFailure)
            {
                return Result<RegistrationSummary>.Fail(released.Errors);
            }

            _registrations.Update(registration);

            var action = withdraw ? RegistrationsAuditActions.EnrollmentWithdrawn : RegistrationsAuditActions.EnrollmentDropped;
            var committed = await CommitAsync(registration, action, before, ";Enrollments=" + string.Join(',', left.Select(e => e.Id)), request, cancellationToken);

            return committed.IsFailure ? Result<RegistrationSummary>.Fail(committed.Errors) : RegistrationMapper.ToSummary(registration);
        }

        // ---------- helpers ----------

        private sealed record ChangeContext(Registration Registration, Student Student, bool IsStaff);

        // Permission alone -> load -> scope. The registration is loaded tracked, with its enrollments.
        private async Task<Result<ChangeContext>> LoadForChangeAsync(Guid registrationId, CancellationToken cancellationToken)
        {
            var permission = await _authorizer.AuthorizeAsync(Permissions.Registration.Register, null, cancellationToken);
            if (permission.IsFailure)
            {
                var staff = await _authorizer.AuthorizeAsync(Permissions.Registration.Manage, null, cancellationToken);
                if (staff.IsFailure)
                {
                    return Result<ChangeContext>.Fail(permission.Errors);
                }
            }

            var registration = await _registrations.FirstOrDefaultAsync(new RegistrationByIdSpecification(registrationId, tracking: true), cancellationToken);
            if (registration is null)
            {
                return RegistrationsErrors.RegistrationNotFound;
            }

            var scope = await _access.AuthorizeAsync(registration.StudentId, write: true, cancellationToken);

            return scope.TryGetValue(out var student) ? new ChangeContext(registration, student.Student, student.IsStaff) : Result<ChangeContext>.Fail(scope.Errors);
        }

        // Registration-only (cancel by a student) or add/drop (drop), or the withdrawal period (withdraw).
        private async Task<Result> RequireWindowAsync(Guid semesterId, bool registrationWindowOnly, CancellationToken cancellationToken, bool withdraw = false)
        {
            if (withdraw)
            {
                var withdrawal = await _calendar.IsWithdrawalOpenAsync(semesterId, cancellationToken);
                if (!withdrawal.TryGetValue(out var withdrawalOpen))
                {
                    return Result.Fail(withdrawal.Errors);
                }

                return withdrawalOpen ? Result.Ok() : Result.Fail(RegistrationsErrors.WithdrawalDeadlinePassed);
            }

            var registrationOpen = await _calendar.IsRegistrationOpenAsync(semesterId, cancellationToken);
            if (!registrationOpen.TryGetValue(out var isOpen))
            {
                return Result.Fail(registrationOpen.Errors);
            }

            if (isOpen)
            {
                return Result.Ok();
            }

            if (registrationWindowOnly)
            {
                return Result.Fail(RegistrationsErrors.Closed);
            }

            var addDrop = await _calendar.IsAddDropOpenAsync(semesterId, cancellationToken);
            if (!addDrop.TryGetValue(out var addDropOpen))
            {
                return Result.Fail(addDrop.Errors);
            }

            return addDropOpen ? Result.Ok() : Result.Fail(RegistrationsErrors.AddDropClosed);
        }

        // Another registered course (not leaving in the same request) that names a leaving course as corequisite blocks it.
        private async Task<Result> CheckCorequisiteDropAsync(Registration registration, IReadOnlyList<Enrollment> leaving, CancellationToken cancellationToken)
        {
            var leavingIds = leaving.Select(e => e.Id).ToHashSet();
            var leavingCourses = leaving.Where(e => e.HoldsSeat).Select(e => e.CourseId).ToHashSet();

            if (leavingCourses.Count == 0)
            {
                return Result.Ok();
            }

            foreach (var staying in registration.RegisteredEnrollments.Where(e => !leavingIds.Contains(e.Id)))
            {
                var needs = await _prerequisites.GetCorequisiteCourseIdsAsync(staying.CourseVersionId, cancellationToken);
                if (!needs.TryGetValue(out var corequisites))
                {
                    return Result.Fail(needs.Errors);
                }

                if (corequisites.Any(leavingCourses.Contains))
                {
                    return Result.Fail(RegistrationsErrors.CorequisiteDropBlocked);
                }
            }

            return Result.Ok();
        }

        // One ReleaseSeat per enrollment that held a seat; saved by the caller with the registration.
        private async Task<Result> ReleaseSeatsAsync(IEnumerable<Enrollment> enrollments, CancellationToken cancellationToken)
        {
            var now = _clock.UtcNow;

            foreach (var enrollment in enrollments)
            {
                var section = await _sections.GetByIdAsync(enrollment.CourseSectionId, cancellationToken);
                if (section is null)
                {
                    return Result.Fail(RegistrationsErrors.SectionNotFound);
                }

                var released = section.ReleaseSeat(now);
                if (released.IsFailure)
                {
                    return released;
                }

                _sections.Update(section);
            }

            return Result.Ok();
        }
    }
}
