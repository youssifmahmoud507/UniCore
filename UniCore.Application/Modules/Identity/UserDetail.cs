namespace UniCore.Application.Modules.Identity
{
    public sealed record UserDetail(UserSummary User, IReadOnlyList<ScopeAssignmentInfo> Scopes);



}
