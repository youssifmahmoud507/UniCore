namespace UniCore.Application.Modules.Identity
{
    public sealed record RoleSummary(Guid Id, string Name, int PermissionCount, int UserCount, bool IsBuiltIn);



}
