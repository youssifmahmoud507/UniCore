namespace UniCore.Application.Modules.Identity
{
    public sealed record RoleDetail(Guid Id, string Name, bool IsBuiltIn, IReadOnlyList<string> Permissions);



}
