using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Graduation;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Graduation
{
    // The checks every handler starts with: the permission alone, then the student, then the scope.
    // A student reaches their own records (ownership); heads and deans the students of their department (through the program);
    // decisions need the permission with a whole-university scope.
    public sealed class GraduationAccess(IPermissionAuthorizer authorizer, IAggregateRepository<Student> students, IRepository<AcademicProgram> programs)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IRepository<AcademicProgram> _programs = programs;

        public Task<Result> RequireAsync(string permission, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, null, cancellationToken);

        public Task<Result> RequireUniversityWideAsync(string permission, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, new ResourceContext(ScopeType.Global), cancellationToken);

        public async Task<Result<Student>> LoadStudentAsync(Guid studentId, bool tracking, CancellationToken cancellationToken)
        {
            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId, tracking), cancellationToken);

            return student is null ? GraduationErrors.StudentNotFound : student;
        }

        public async Task<Result<Student>> AuthorizeStudentAsync(string permission, Guid studentId, CancellationToken cancellationToken)
        {
            var allowed = await RequireAsync(permission, cancellationToken);
            if (allowed.IsFailure)
            {
                return Result<Student>.Fail(allowed.Errors);
            }

            var loaded = await LoadStudentAsync(studentId, tracking: false, cancellationToken);
            if (!loaded.TryGetValue(out var student))
            {
                return loaded;
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
    }
}
