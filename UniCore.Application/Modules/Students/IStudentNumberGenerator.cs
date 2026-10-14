using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Students
{
    public interface IStudentNumberGenerator
    {
        // Concurrency safe. Gaps are possible (a number is lost if the save that follows fails).
        Task<Result<string>> NextAsync(int admissionYear, string? programCode, CancellationToken cancellationToken = default);
    }


}
