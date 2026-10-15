using UniCore.Application.Modules.University;
using UniCore.Domain.Common.Results;

namespace UniCore.UnitTests.Modules.University
{
    internal sealed class FakeProgramUsageChecker : IProgramUsageChecker
    {
        public bool HasStudents { get; set; }
        public Error? FailWith { get; set; }

        public Task<Result<bool>> HasActiveStudentsAsync(Guid academicProgramId, CancellationToken cancellationToken = default)
            => Task.FromResult(FailWith is null ? Result<bool>.Ok(HasStudents) : Result<bool>.Fail(FailWith));
    }
}
