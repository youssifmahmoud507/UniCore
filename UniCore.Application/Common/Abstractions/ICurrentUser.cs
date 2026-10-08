namespace UniCore.Application.Common.Abstractions
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }
        Guid? UserId { get; }
        Guid? PersonId { get; }
        IReadOnlyCollection<string> Roles { get; }
        IReadOnlyCollection<string> Permissions { get; }
        IReadOnlyCollection<ScopeGrant> Scopes { get; }

        bool IsInRole(string role);
        bool HasPermission(string permission);
    }
}
