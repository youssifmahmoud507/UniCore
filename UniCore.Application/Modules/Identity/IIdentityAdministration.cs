using UniCore.Application.Common.Pagination;
using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Identity
{
    // Everything that needs Identity (users, roles, role membership).
    // Methods marked "stages" change tracked data without saving: the handler saves everything
    // together through IUnitOfWork, so one operation is one transaction.
    public interface IIdentityAdministration
    {
        Task<Result<IReadOnlyList<RoleSummary>>> ListRolesAsync(CancellationToken cancellationToken = default);
        Task<Result<RoleBasic>> FindRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
        Task<Result<RoleBasic>> FindRoleByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<Result<int>> CountUsersInRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
        Task<Result<int>> CountActiveUsersInRoleAsync(Guid roleId, Guid excludingUserId, CancellationToken cancellationToken = default);

        // Saves immediately (Identity manages its own save).
        Task<Result<RoleBasic>> CreateRoleAsync(string? name);

        // Saves immediately, together with anything the handler staged before calling it.
        Task<Result> DeleteRoleAsync(Guid roleId);

        // Saves immediately. The user has no password: it is set through the forgot-password flow.
        Task<Result<UserSummary>> CreateUserAsync(string? userName, string? email, string? phoneNumber, DateTimeOffset now);

        Task<Result<UserDetail>> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<Result<PagedResult<UserSummary>>> ListUsersAsync(PaginationQuery query, string? search, bool? isActive, CancellationToken cancellationToken = default);

        // Stages.
        Task<Result> SetActiveAsync(Guid userId, bool active, DateTimeOffset now, CancellationToken cancellationToken = default);

        // Stages. The result is true when the membership was added, false when it already existed.
        Task<Result<bool>> AddUserToRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

        // Stages. The result is true when the membership was removed, false when it did not exist.
        Task<Result<bool>> RemoveUserFromRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    }



}
