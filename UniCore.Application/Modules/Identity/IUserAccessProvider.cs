using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Identity
{
    public interface IUserAccessProvider
    {
        // Roles, permissions and scopes of a user. An inactive or unknown user gets no grants.
        Task<Result<UserAccess>> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    }



}
