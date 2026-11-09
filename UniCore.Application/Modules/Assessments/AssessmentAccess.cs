using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Assessments
{
    // The checks every handler of this module starts with. Order: the permission alone, then load, then the scope.
    // The grading scheme is scoped by the section: Assessments.Read / Manage on the section (an instructor assigned to it)
    // or on the department of its course (head, dean) or globally (registrar, read only by default).
    public sealed class AssessmentAccess(
        IPermissionAuthorizer authorizer, IOfferingReadService offerings, ICourseCatalogReadService catalog, IRepository<AcademicSemester> semesters)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IOfferingReadService _offerings = offerings;
        private readonly ICourseCatalogReadService _catalog = catalog;
        private readonly IRepository<AcademicSemester> _semesters = semesters;

        public sealed record SectionContext(SectionInfo Section, Guid DepartmentId, AcademicSemester Semester);

        public Task<Result> RequireAsync(string permission, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, null, cancellationToken);

        public async Task<Result<SectionContext>> AuthorizeSectionAsync(string permission, Guid sectionId, bool openSemester, CancellationToken cancellationToken)
        {
            var loaded = await LoadSectionAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return loaded;
            }

            var byDepartment = await _authorizer.AuthorizeAsync(permission, ResourceContext.InScope(ScopeType.Department, context.DepartmentId), cancellationToken);
            if (byDepartment.IsFailure)
            {
                var bySection = await _authorizer.AuthorizeAsync(permission, ResourceContext.InScope(ScopeType.Section, sectionId), cancellationToken);
                if (bySection.IsFailure)
                {
                    return Result<SectionContext>.Fail(byDepartment.Errors);
                }
            }

            return openSemester && context.Semester.IsClosed ? AssessmentErrors.SemesterClosed : context;
        }

        // No permission or scope check: for services that were already authorized.
        public async Task<Result<SectionContext>> LoadSectionAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            var section = await _offerings.GetSectionAsync(sectionId, cancellationToken);
            if (!section.TryGetValue(out var info))
            {
                return AssessmentErrors.SectionNotFound;
            }

            var version = await _catalog.GetVersionAsync(info.CourseVersionId, cancellationToken);
            if (!version.TryGetValue(out var courseVersion))
            {
                return AssessmentErrors.SectionNotFound;
            }

            var semester = await _semesters.GetByIdAsync(info.AcademicSemesterId, cancellationToken);

            return semester is null ? AssessmentErrors.SemesterNotFound : new SectionContext(info, courseVersion.DepartmentId, semester);
        }

        // The exam window of the semester for a midterm or final component (null when the semester has none or for other types).
        public static (DateOnly Start, DateOnly End)? WindowOf(AcademicSemester semester, ComponentType type)
            => type switch
            {
                ComponentType.Midterm => semester.MidtermStart is { } ms && semester.MidtermEnd is { } me ? (ms, me) : null,
                ComponentType.Final => semester.FinalExamStart is { } fs && semester.FinalExamEnd is { } fe ? (fs, fe) : null,
                _ => null
            };

        // A due date must be inside the semester, and a midterm or final one inside its exam window.
        public static Result CheckDueDate(AcademicSemester semester, ComponentType type, DateOnly? dueDate)
        {
            if (dueDate is not { } date)
            {
                return Result.Ok();
            }

            if (date < semester.StartDate || date > semester.EndDate)
            {
                return Result.Fail(AssessmentErrors.DueDateOutsideSemester);
            }

            return WindowOf(semester, type) is { } window && (date < window.Start || date > window.End)
                ? Result.Fail(AssessmentErrors.DueDateOutsideWindow)
                : Result.Ok();
        }
    }
}
