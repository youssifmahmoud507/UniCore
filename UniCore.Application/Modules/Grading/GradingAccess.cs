using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Offerings;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Calendar;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Grading
{
    // The checks every handler of this module starts with. Order: the permission alone, then load, then the scope.
    // Section scope: an instructor holds a Section grant for each section they are assigned to (InstructorAssignment,
    // Week 8, through People's scope data), so Attendance.Record works only on their own sections. Heads and deans
    // reach the section through the department of its course. Students: ownership of their own records.
    public sealed class GradingAccess(
        IPermissionAuthorizer authorizer, IOfferingReadService offerings, ICourseCatalogReadService catalog,
        IRepository<AcademicSemester> semesters, IAggregateRepository<Student> students, IRepository<AcademicProgram> programs)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IOfferingReadService _offerings = offerings;
        private readonly ICourseCatalogReadService _catalog = catalog;
        private readonly IRepository<AcademicSemester> _semesters = semesters;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IRepository<AcademicProgram> _programs = programs;

        public sealed record SectionContext(SectionInfo Section, Guid DepartmentId, AcademicSemester Semester);

        public Task<Result> RequireAsync(string permission, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, null, cancellationToken);

        // The section must exist and the caller must hold the permission on it. With openSemester the semester must not be closed.
        public async Task<Result<SectionContext>> AuthorizeSectionAsync(string permission, Guid sectionId, bool openSemester, CancellationToken cancellationToken)
        {
            var loaded = await LoadSectionAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return loaded;
            }

            var bySection = await _authorizer.AuthorizeAsync(permission, ResourceContext.InScope(ScopeType.Section, sectionId), cancellationToken);
            if (bySection.IsFailure)
            {
                var byDepartment = await _authorizer.AuthorizeAsync(permission, ResourceContext.InScope(ScopeType.Department, context.DepartmentId), cancellationToken);
                if (byDepartment.IsFailure)
                {
                    return Result<SectionContext>.Fail(bySection.Errors);
                }
            }

            return openSemester && context.Semester.IsClosed ? GradingErrors.SemesterClosed : context;
        }

        // No permission or scope check: for services that were already authorized (system jobs, other modules).
        public async Task<Result<SectionContext>> LoadSectionAsync(Guid sectionId, CancellationToken cancellationToken)
        {
            var section = await _offerings.GetSectionAsync(sectionId, cancellationToken);
            if (!section.TryGetValue(out var info))
            {
                return GradingErrors.SectionNotFound;
            }

            var version = await _catalog.GetVersionAsync(info.CourseVersionId, cancellationToken);
            if (!version.TryGetValue(out var courseVersion))
            {
                return GradingErrors.SectionNotFound;
            }

            var semester = await _semesters.GetByIdAsync(info.AcademicSemesterId, cancellationToken);

            return semester is null ? GradingErrors.SectionNotFound : new SectionContext(info, courseVersion.DepartmentId, semester);
        }

        // The student's own account, or the department of the student's current program. Permission alone comes first.
        public async Task<Result<Student>> AuthorizeStudentAsync(string permission, Guid studentId, CancellationToken cancellationToken)
        {
            var allowed = await RequireAsync(permission, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<Student>.Fail(allowed.Errors);
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);
            if (student is null)
            {
                return GradingErrors.StudentNotFound;
            }

            var resource = ResourceContext.OwnedBy();
            if (student.UserId is { } userId)
            {
                resource = resource.WithOwners(userId);
            }

            if (student.CurrentProgram is { } current)
            {
                var program = await _programs.GetByIdAsync(current.AcademicProgramId, cancellationToken);
                if (program is not null)
                {
                    resource = resource with { ScopeType = ScopeType.Department, ScopeId = program.DepartmentId };
                }
            }

            var scoped = await _authorizer.AuthorizeAsync(permission, resource, cancellationToken);

            return scoped.IsFailure ? Result<Student>.Fail(scoped.Errors) : student;
        }

        // Grades are entered only by the instructor assigned to the section (a Section grant); heads and deans do not enter grades.
        public async Task<Result<SectionContext>> AuthorizeAssignedInstructorAsync(string permission, Guid sectionId, bool openSemester, CancellationToken cancellationToken)
        {
            var loaded = await LoadSectionAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return loaded;
            }

            var bySection = await _authorizer.AuthorizeAsync(permission, ResourceContext.InScope(ScopeType.Section, sectionId), cancellationToken);
            if (bySection.IsFailure)
            {
                return Result<SectionContext>.Fail(bySection.Errors);
            }

            return openSemester && context.Semester.IsClosed ? GradingErrors.SemesterClosed : context;
        }

        // Review, approve, publish and lock: the department of the section's course (head, dean) or global (registrar).
        public async Task<Result<SectionContext>> AuthorizeDepartmentAsync(string permission, Guid sectionId, bool openSemester, CancellationToken cancellationToken)
        {
            var loaded = await LoadSectionAsync(sectionId, cancellationToken);
            if (!loaded.TryGetValue(out var context))
            {
                return loaded;
            }

            var byDepartment = await _authorizer.AuthorizeAsync(permission, ResourceContext.InScope(ScopeType.Department, context.DepartmentId), cancellationToken);
            if (byDepartment.IsFailure)
            {
                return Result<SectionContext>.Fail(byDepartment.Errors);
            }

            return openSemester && context.Semester.IsClosed ? GradingErrors.SemesterClosed : context;
        }
    }
}
