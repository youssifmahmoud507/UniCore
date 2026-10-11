using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;
using UniCore.UnitTests.Common;

namespace UniCore.UnitTests.Modules.Identity
{
    // Wires the real handlers and the real PermissionAuthorizer to in-memory fakes.
    internal sealed class RbacTestEnv
    {
        public FakeClock Clock { get; } = new(AuthTestEnv.Start);
        public TestUserAccessContext Access { get; } = new();
        public TestCurrentUser User { get; }
        public FakeScopeHierarchy Hierarchy { get; } = new();

        public InMemoryRepository<Permission> PermissionRows { get; } = new();
        public InMemoryRepository<RolePermission> RolePermissionRows { get; } = new();
        public InMemoryRepository<ScopeAssignment> ScopeRows { get; } = new();
        public InMemoryRepository<RefreshToken> RefreshTokenRows { get; } = new();

        public FakeUnitOfWork Uow { get; } = new();
        public FakeAuditWriter Audit { get; } = new();
        public FakeEmailQueue Emails { get; } = new();
        public TestPasswordResetSettings Settings { get; } = new();
        public FakeIdentityAdministration Identity { get; }

        public PermissionAuthorizer Authorizer { get; }
        public RolePermissionCatalog Catalog { get; }
        public RoleManagementHandler RoleHandler { get; }
        public UserManagementHandler UserHandler { get; }

        public Guid ActorId { get; } = Guid.NewGuid();
        public Dictionary<string, Guid> RoleIds { get; } = new(StringComparer.Ordinal);

        private readonly Dictionary<string, Guid> _permissionIds = new(StringComparer.Ordinal);

        public RbacTestEnv()
        {
            User = new TestCurrentUser(Access);
            Identity = new FakeIdentityAdministration(RolePermissionRows, ScopeRows);

            Authorizer = new PermissionAuthorizer(User, Access, Hierarchy);
            Catalog = new RolePermissionCatalog(PermissionRows, RolePermissionRows);

            RoleHandler = new RoleManagementHandler(Authorizer, User, Identity, PermissionRows, RolePermissionRows, Catalog, Uow, Audit);

            UserHandler = new UserManagementHandler(Authorizer, User, Identity, Catalog, ScopeRows, RefreshTokenRows, Uow, Clock, Emails, Settings, Audit);

            foreach (var name in RoleNames.All)
            {
                RoleIds[name] = Identity.SeedRole(name);
            }

            foreach (var definition in Permissions.All)
            {
                var created = Permission.Create(definition.Code, definition.Code, definition.Description, definition.Module);
                Assert.True(created.TryGetValue(out var permission));
                Assert.NotNull(permission);

                PermissionRows.Add(permission);
                _permissionIds[definition.Code] = permission.Id;
            }

            foreach (var (roleName, codes) in DefaultRolePermissions.ByRole)
            {
                LinkPermissions(RoleIds[roleName], codes.ToArray());
            }
        }

        public RequestInfo Request { get; } = new("10.0.0.1", "agent");

        public void SignIn(Guid userId, params RoleGrant[] grants)
        {
            User.UserId = userId;
            Access.Set(new UserAccess(userId, grants));
        }

        // The actor is also stored as an active user, like a real signed-in SuperAdmin.
        public void SignInAsSuperAdmin(bool registerActorAsUser = true)
        {
            if (registerActorAsUser)
            {
                Identity.SeedUserWithId(ActorId, "root", true, RoleNames.SuperAdmin);
            }

            SignIn(ActorId, TestGrants.SuperAdmin());
        }

        public Guid AddCustomRole(string name, params string[] codes)
        {
            var id = Identity.SeedRole(name);
            LinkPermissions(id, codes);
            return id;
        }

        public RefreshToken AddRefreshToken(Guid userId)
        {
            var created = RefreshToken.Create(
                userId, Guid.NewGuid().ToString("N"), Clock.UtcNow, Clock.UtcNow.AddDays(7), null);

            Assert.True(created.TryGetValue(out var token));
            Assert.NotNull(token);

            RefreshTokenRows.Add(token);
            return token;
        }

        public int PermissionCountOf(Guid roleId) => RolePermissionRows.Items.Count(rp => rp.RoleId == roleId);

        public IEnumerable<AuditEntry> AuditOf(string action) => Audit.Entries.Where(e => e.Action == action);

        private void LinkPermissions(Guid roleId, string[] codes)
        {
            foreach (var code in codes)
            {
                var link = RolePermission.Create(roleId, _permissionIds[code]);
                Assert.True(link.TryGetValue(out var rolePermission));
                Assert.NotNull(rolePermission);

                RolePermissionRows.Add(rolePermission);
            }
        }
    }

}
