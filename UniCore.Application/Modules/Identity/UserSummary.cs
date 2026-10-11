namespace UniCore.Application.Modules.Identity
{
    public sealed record UserSummary(Guid Id,string UserName,string Email,bool IsActive,DateTimeOffset CreatedAt,DateTimeOffset? LastLoginAt,IReadOnlyList<string> Roles);



}
