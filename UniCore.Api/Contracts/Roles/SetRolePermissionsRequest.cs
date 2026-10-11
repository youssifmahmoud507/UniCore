namespace UniCore.Api.Contracts.Roles
{
    public sealed record SetRolePermissionsRequest(IReadOnlyList<string>? Permissions);
}
