using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Examination
{
    // The checks every handler of this module starts with. Order: the permission alone, then load, then the scope.
    // Exams are scoped by the section: Exams.Read on the section (an instructor assigned to it) or on the department of its
    // course; Exams.Manage on the department of its course (or globally, the registrar). Sessions are university-wide:
    // the permission alone decides.
    public sealed class ExaminationAccess(
        IPermissionAuthorizer authorizer, IOfferingReadService offerings, ICourseCatalogReadService catalog, IRepository<AcademicSemester> semesters)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IOfferingReadService _offerings = offerings;
        private readonly ICourseCatalogReadService _catalog = catalog;
        private readonly IRepository<AcademicSemester> _semesters = semesters;

        public sealed record SectionContext(SectionInfo Section, Guid DepartmentId, AcademicSemester Semester);

        public Task<Result> RequireAsync(string permission, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, null, cancellationToken);

        public async Task<Result<AcademicSemester>> LoadSemesterAsync(Guid semesterId, bool open, CancellationToken cancellationToken)
        {
            var semester = semesterId == Guid.Empty ? null : await _semesters.GetByIdAsync(semesterId, cancellationToken);
            if (semester is null)
            {
                return ExaminationErrors.SemesterNotFound;
            }

            return open && semester.IsClosed ? ExaminationErrors.SemesterClosed : semester;
        }

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

            return openSemester && context.Semester.IsClosed ? ExaminationErrors.SemesterClosed : context;
        }

        // No permission or scope check: for services that were already authorized.
        public async Task<Result<SectionContext>> LoadSectionAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            var section = await _offerings.GetSectionAsync(sectionId, cancellationToken);
            if (!section.TryGetValue(out var info))
            {
                return ExaminationErrors.SectionNotFound;
            }

            var version = await _catalog.GetVersionAsync(info.CourseVersionId, cancellationToken);
            if (!version.TryGetValue(out var courseVersion))
            {
                return ExaminationErrors.SectionNotFound;
            }

            var semester = await _semesters.GetByIdAsync(info.AcademicSemesterId, cancellationToken);

            return semester is null ? ExaminationErrors.SemesterNotFound : new SectionContext(info, courseVersion.DepartmentId, semester);
        }

        // The window of the semester for an exam type (null when the semester has none).
        public static (DateOnly Start, DateOnly End)? WindowOf(AcademicSemester semester, ExamType type)
            => type == ExamType.Midterm
                ? semester.MidtermStart is { } ms && semester.MidtermEnd is { } me ? (ms, me) : null
                : semester.FinalExamStart is { } fs && semester.FinalExamEnd is { } fe ? (fs, fe) : null;
    }
}
