using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Calendar;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Curriculum;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Offerings;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Registrations
{
    // What the pipeline needs to judge one request. Registration is null when the student has none yet this semester.
    public sealed record PipelineInput(Student Student, AcademicSemester Semester, Guid CourseSectionId, Registration? Registration, bool SkipRegistrationWindow);

    // The ordered checks of registration-rules.md section 2. It changes nothing: the first failing step returns its
    // error and stops, so the answer to "why not?" is always the earliest reason. The seat itself (the last step) is
    // taken by the handler, in the same save as the enrollment.
    //
    //   0 section exists, is open and is in this semester
    //   1 student is active
    //   2 registration window is open
    //   3 no financial hold
    //   4 the course is in the student's study plan
    //   5 prerequisites met, corequisites passed or registered together
    //   6 not already passed (unless grade improvement is allowed)
    //   7 not already enrolled this semester
    //   8 credit limit
    //   9 no schedule conflict
    public sealed class RegistrationPipeline(
        IOfferingReadService offerings, IAcademicCalendarService calendar, IFinancialEligibilityService finance,
        IStudyPlanReadService plans, ICourseCatalogReadService catalog, IPrerequisiteEvaluator prerequisites,
        IStudentCourseHistory history, IScheduleConflictService schedule, IRegistrationSettings settings, IRepository<AcademicTerm> terms)
    {
        private readonly IOfferingReadService _offerings = offerings;
        private readonly IAcademicCalendarService _calendar = calendar;
        private readonly IFinancialEligibilityService _finance = finance;
        private readonly IStudyPlanReadService _plans = plans;
        private readonly ICourseCatalogReadService _catalog = catalog;
        private readonly IPrerequisiteEvaluator _prerequisites = prerequisites;
        private readonly IStudentCourseHistory _history = history;
        private readonly IScheduleConflictService _schedule = schedule;
        private readonly IRegistrationSettings _settings = settings;
        private readonly IRepository<AcademicTerm> _terms = terms;

        public async Task<Result<SectionInfo>> CheckAsync(PipelineInput input, CancellationToken cancellationToken = default)
        {
            var student = input.Student;
            var registration = input.Registration;

            // 0. The section.
            var found = await _offerings.GetSectionAsync(input.CourseSectionId, cancellationToken);
            if (!found.TryGetValue(out var section))
            {
                return RegistrationsErrors.SectionNotFound;
            }

            if (section.AcademicSemesterId != input.Semester.Id)
            {
                return RegistrationsErrors.SectionSemesterMismatch;
            }

            if (!section.IsOpenForEnrollment)
            {
                return RegistrationsErrors.SectionNotOpen;
            }

            var instructors = await _offerings.GetActiveInstructorsAsync(section.SectionId, cancellationToken);
            if (!instructors.TryGetValue(out var assigned))
            {
                return Result<SectionInfo>.Fail(instructors.Errors);
            }

            // A section nobody teaches is not offered yet.
            if (!assigned.Any(i => i.Role == AssignmentRole.Primary))
            {
                return RegistrationsErrors.SectionNotOpen;
            }

            // 1. The student.
            if (student.AcademicStatus != StudentAcademicStatus.Active)
            {
                return RegistrationsErrors.StudentInactive;
            }

            // 2. The window (registration staff may skip it with a reason).
            if (!input.SkipRegistrationWindow)
            {
                var window = await _calendar.IsRegistrationOpenAsync(input.Semester.Id, cancellationToken);
                if (!window.TryGetValue(out var open))
                {
                    return Result<SectionInfo>.Fail(window.Errors);
                }

                if (!open)
                {
                    return RegistrationsErrors.Closed;
                }
            }

            // 3. Financial hold.
            var hold = await _finance.HasBlockingHoldAsync(student.Id, FinancialHoldArea.Registration, cancellationToken);
            if (!hold.TryGetValue(out var blocked))
            {
                return Result<SectionInfo>.Fail(hold.Errors);
            }

            if (blocked)
            {
                return RegistrationsErrors.FinancialHold;
            }

            // 4. Study plan.
            var inPlan = await IsInStudyPlanAsync(student, section, cancellationToken);
            if (!inPlan.TryGetValue(out var planned))
            {
                return Result<SectionInfo>.Fail(inPlan.Errors);
            }

            if (!planned)
            {
                return RegistrationsErrors.CourseNotInStudyPlan;
            }

            // 5. Prerequisites, then corequisites.
            var prerequisitesMet = await _prerequisites.ArePrerequisitesMetAsync(student.Id, section.CourseVersionId, cancellationToken);
            if (!prerequisitesMet.TryGetValue(out var met))
            {
                return Result<SectionInfo>.Fail(prerequisitesMet.Errors);
            }

            if (!met)
            {
                return RegistrationsErrors.PrerequisiteNotMet;
            }

            var corequisites = await _prerequisites.GetCorequisiteCourseIdsAsync(section.CourseVersionId, cancellationToken);
            if (!corequisites.TryGetValue(out var corequisiteIds))
            {
                return Result<SectionInfo>.Fail(corequisites.Errors);
            }

            foreach (var corequisiteId in corequisiteIds)
            {
                if (registration?.HasRegisteredCourse(corequisiteId) == true)
                {
                    continue;
                }

                var attempts = await _history.GetAttemptsAsync(student.Id, corequisiteId, cancellationToken);
                if (!attempts.TryGetValue(out var summary))
                {
                    return Result<SectionInfo>.Fail(attempts.Errors);
                }

                if (!summary.Passed)
                {
                    return RegistrationsErrors.CorequisiteNotMet;
                }
            }

            // 6. Already passed.
            var own = await _history.GetAttemptsAsync(student.Id, section.CourseId, cancellationToken);
            if (!own.TryGetValue(out var ownAttempts))
            {
                return Result<SectionInfo>.Fail(own.Errors);
            }

            if (ownAttempts.Passed && !CanImprove(ownAttempts))
            {
                return RegistrationsErrors.CourseAlreadyPassed;
            }

            // 7. Already enrolled in this course this semester (any section).
            if (registration?.HasRegisteredCourse(section.CourseId) == true)
            {
                return RegistrationsErrors.AlreadyEnrolled;
            }

            // 8. Credit limit.
            var limit = await CreditLimitAsync(student, input.Semester, cancellationToken);
            if (!limit.TryGetValue(out var maxCredits))
            {
                return Result<SectionInfo>.Fail(limit.Errors);
            }

            if ((registration?.RegisteredCredits ?? 0) + section.Credits > maxCredits)
            {
                return RegistrationsErrors.CreditLimitExceeded;
            }

            // 9. Schedule conflict.
            IReadOnlyCollection<Guid> held = registration is null ? [] : [.. registration.RegisteredEnrollments.Select(e => e.CourseSectionId)];
            var conflict = await _schedule.HasConflictAsync(student.Id, input.Semester.Id, section.SectionId, held, cancellationToken);
            if (!conflict.TryGetValue(out var overlaps))
            {
                return Result<SectionInfo>.Fail(conflict.Errors);
            }

            if (overlaps)
            {
                return RegistrationsErrors.ScheduleConflict;
            }

            return section;
        }

        // The credit limit that applies to this student in this semester: summer first, then probation, then regular.
        public async Task<Result<int>> CreditLimitAsync(Student student, AcademicSemester semester, CancellationToken cancellationToken = default)
        {
            var term = await _terms.GetByIdAsync(semester.AcademicTermId, cancellationToken);
            if (term is null)
            {
                return Result<int>.Fail(RegistrationsErrors.SemesterNotFound);
            }

            if (string.Equals(term.Code, _settings.SummerTermCode, StringComparison.OrdinalIgnoreCase))
            {
                return _settings.SummerCreditLimit;
            }

            return student.Profile?.AcademicStanding == AcademicStanding.Probation ? _settings.ProbationCreditLimit : _settings.RegularCreditLimit;
        }

        private bool CanImprove(CourseAttemptSummary attempts)
            => _settings.AllowGradeImprovement && attempts.BestGradePoint is { } best && best < _settings.ImprovementMaxGradePoint;

        // The plan lists course VERSIONS; the section may belong to a newer version of the same course, so the
        // fallback compares courses.
        private async Task<Result<bool>> IsInStudyPlanAsync(Student student, SectionInfo section, CancellationToken cancellationToken)
        {
            var planVersionId = student.Profile?.CurrentStudyPlanVersionId;
            if (planVersionId is not { } versionId)
            {
                return false;
            }

            var direct = await _plans.ContainsCourseAsync(versionId, section.CourseVersionId, cancellationToken);
            if (!direct.TryGetValue(out var contained))
            {
                return direct;
            }

            if (contained)
            {
                return true;
            }

            var courses = await _plans.GetCoursesAsync(versionId, cancellationToken);
            if (!courses.TryGetValue(out var items))
            {
                return Result<bool>.Fail(courses.Errors);
            }

            foreach (var item in items)
            {
                var version = await _catalog.GetVersionAsync(item.CourseVersionId, cancellationToken);
                if (version.TryGetValue(out var info) && info.CourseId == section.CourseId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
