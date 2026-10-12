using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Students
{
    public interface IStudentAccountProvisioner
    {
        // Creates (or reuses) a login account without a password and gives it the Student role.
        // Safe to call again after a failure.
        Task<Result<Guid>> ProvisionAsync(string userName, string email, DateTimeOffset now, CancellationToken cancellationToken = default);
    }


}
