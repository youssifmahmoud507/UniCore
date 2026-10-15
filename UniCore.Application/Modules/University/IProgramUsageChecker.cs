using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.University
{
    // Implemented in Infrastructure. Lets this module ask the Students module's data a yes/no question
    // without reaching into Student types.
    public interface IProgramUsageChecker
    {
        // True while at least one student has an open (not ended) program history row for the program.
        // A database failure comes back as a failed Result, never as "false".
        Task<Result<bool>> HasActiveStudentsAsync(Guid academicProgramId, CancellationToken cancellationToken = default);
    }
}
