using UniCore.Application.Common.Abstractions;

namespace UniCore.UnitTests.Modules.Identity
{
    // Mirrors the real CurrentUser: roles, permissions and scopes come from the loaded access.
    internal sealed class TestCurrentUser(TestUserAccessContext access) : ICurrentUser
    {
        private readonly TestUserAccessContext _access = access;

        public Guid? UserId { get; set; }
        public bool IsAuthenticated => UserId is not null;
        public Guid? PersonId => null;

        public IReadOnlyCollection<string> Roles => _access.Current is { } a ? [.. a.Grants.Select(g => g.Role).Distinct()] : Array.Empty<string>();
        public IReadOnlyCollection<string> Permissions => _access.Current is { } a ? a.Grants.SelectMany(g => g.Permissions).Distinct().ToArray() : [];
        public IReadOnlyCollection<ScopeGrant> Scopes => _access.Current is { } a ? a.Grants.SelectMany(g => g.Scopes).Distinct().ToArray() : [];
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasPermission(string permission) => IsAuthenticated && Permissions.Contains(permission);
    }

}
