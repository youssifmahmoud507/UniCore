namespace UniCore.Application.Modules.Identity
{
    public sealed record UserAccess(Guid UserId, IReadOnlyList<RoleGrant> Grants);



}
