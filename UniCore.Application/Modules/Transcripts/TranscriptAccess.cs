using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;
using UniCore.Domain.Modules.Transcripts;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.Transcripts
{
    // The checks every handler of this module starts with: the permission alone, then the student, then the scope.
    // A student reaches their own records (ownership); heads and deans reach the students of their department through the program;
    // the registrar holds the permission globally.
    public sealed class TranscriptAccess(IPermissionAuthorizer authorizer, IAggregateRepository<Student> students, IRepository<AcademicProgram> programs)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IRepository<AcademicProgram> _programs = programs;

        public Task<Result> RequireAsync(string permission, CancellationToken cancellationToken)
            => _authorizer.AuthorizeAsync(permission, null, cancellationToken);

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
                return TranscriptErrors.StudentNotFound;
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

        // No permission or scope check: for services that were already authorized (other modules, workflows).
        public async Task<Result<Student>> LoadStudentAsync(Guid studentId, CancellationToken cancellationToken)
        {
            var student = await _students.FirstOrDefaultAsync(new StudentByIdSpecification(studentId), cancellationToken);

            return student is null ? TranscriptErrors.StudentNotFound : student;
        }
    }
}
