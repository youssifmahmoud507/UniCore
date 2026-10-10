using UniCore.Application.Common.Abstractions;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; }
        public bool IsAuthenticated => UserId is not null;
        public Guid? PersonId => null;
        public IReadOnlyCollection<string> Roles => Array.Empty<string>();
        public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
        public IReadOnlyCollection<ScopeGrant> Scopes => Array.Empty<ScopeGrant>();
        public bool IsInRole(string role) => false;
        public bool HasPermission(string permission) => false;
    }
}
