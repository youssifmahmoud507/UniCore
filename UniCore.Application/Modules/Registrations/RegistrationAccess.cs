using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Registrations
{
    // The checks every handler of this module starts with. Order: the permission alone, then load the student, then
    // the scope: the student's own account (ownership), or the department of the student's current program.
    // IsStaff is true when the grant that opened the door was Registration.Manage.
    public sealed class RegistrationAccess(IPermissionAuthorizer authorizer, IAggregateRepository<Student> students, IRepository<AcademicProgram> programs)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IRepository<AcademicProgram> _programs = programs;

        public sealed record StudentContext(Student Student, bool IsStaff);

        public Task<Result> RequireReadPermissionAsync(CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(Permissions.Registration.Read, null, cancellationToken);

        public async Task<Result<StudentContext>> AuthorizeAsync(Guid studentId, bool write, CancellationToken cancellationToken)
        {
            string[] permissions = write
                ? [Permissions.Registration.Register, Permissions.Registration.Manage]
                : [Permissions.Registration.Read, Permissions.Registration.Manage];

            // Permission alone first, so a caller without any of them cannot learn whether the id exists.
            var allowed = new List<string>();
            Result? firstDenied = null;

            foreach (var permission in permissions)
            {
                var check = await _authorizer.AuthorizeAsync(permission, null, cancellationToken);
                if (check.IsSuccess)
                {
                    allowed.Add(permission);
                }
                else
                {
                    firstDenied ??= check;
                }
            }

            if (allowed.Count == 0)
            {
                return Result<StudentContext>.Fail(firstDenied!.Errors);
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);
            if (student is null)
            {
                return RegistrationsErrors.StudentNotFound;
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

            Result? lastError = null;

            foreach (var permission in allowed)
            {
                var scoped = await _authorizer.AuthorizeAsync(permission, resource, cancellationToken);
                if (scoped.IsSuccess)
                {
                    return new StudentContext(student, permission == Permissions.Registration.Manage);
                }

                lastError = scoped;
            }

            return Result<StudentContext>.Fail(lastError!.Errors);
        }
    }
}
