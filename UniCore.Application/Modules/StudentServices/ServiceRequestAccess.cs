using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.StudentServices;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.StudentServices
{
    // Who may touch a student's service requests. A student reaches their own (ownership); heads and deans the requests of their
    // department's students (scope through the program); the registrar all of them.
    // On an existing request a refusal is shown as "not found": the caller must not learn that someone else's request exists.
    public sealed class ServiceRequestAccess(IPermissionAuthorizer authorizer, IAggregateRepository<Student> students, IRepository<AcademicProgram> programs)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IRepository<AcademicProgram> _programs = programs;

        public async Task<Result<Student>> AuthorizeStudentAsync(string permission, Guid studentId, bool hideRefusal, CancellationToken cancellationToken)
        {
            var allowed = await _authorizer.AuthorizeAsync(permission, null, cancellationToken);
            if (allowed.IsFailure)
            {
                return Refused(allowed, hideRefusal);
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId, false), cancellationToken);
            if (student is null)
            {
                return ServiceRequestErrors.StudentNotFound;
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

            return scoped.IsFailure ? Refused(scoped, hideRefusal) : student;
        }

        // The permission alone, with a whole-university scope: the staff lists.
        public Task<Result> RequireUniversityWideAsync(string permission, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, new ResourceContext(ScopeType.Global), cancellationToken);

        private static Result<Student> Refused(Result refusal, bool hide)
        {
            if (hide && refusal.Errors.All(e => e.ErrorType == ErrorType.Forbidden))
            {
                return ServiceRequestErrors.NotFound;
            }

            return Result<Student>.Fail(refusal.Errors);
        }
    }
}
