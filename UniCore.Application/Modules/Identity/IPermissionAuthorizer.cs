using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Identity
{
    public interface IPermissionAuthorizer
    {
        // Unauthorized when not signed in, Forbidden when the permission, scope or ownership check fails.
        // Without a resource only the permission itself is checked.
        Task<Result> AuthorizeAsync(string permission, ResourceContext? resource = null, CancellationToken cancellationToken = default);
    }



}
