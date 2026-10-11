using UniCore.Application.Common.Pagination;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class FakeIdentityAdministration(InMemoryRepository<RolePermission> rolePermissions, InMemoryRepository<ScopeAssignment> scopes) : IIdentityAdministration
    {
        private readonly InMemoryRepository<RolePermission> _rolePermissions = rolePermissions;
        private readonly InMemoryRepository<ScopeAssignment> _scopes = scopes;

        public sealed class FakeUser
        {
            public Guid Id { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public DateTimeOffset CreatedAt { get; set; }
        }

        public Dictionary<Guid, string> Roles { get; } = [];
        public List<FakeUser> Users { get; } = new();
        public HashSet<(Guid UserId, Guid RoleId)> Memberships { get; } = [];

        // ---------- test setup helpers ----------

        public Guid SeedRole(string name)
        {
            var id = Guid.NewGuid();
            Roles[id] = name;
            return id;
        }

        public Guid SeedUser(string userName, bool active = true, params string[] roles) => SeedUserWithId(Guid.NewGuid(), userName, active, roles);
        public Guid SeedUserWithId(Guid id, string userName, bool active = true, params string[] roles)
        {
            Users.Add(new FakeUser
            {
                Id = id,
                UserName = userName,
                Email = $"{userName}@uni.edu",
                IsActive = active,
                CreatedAt = AuthTestEnv.Start
            });

            foreach (var role in roles)
            {
                var roleId = Roles.First(r => r.Value == role).Key;
                Memberships.Add((id, roleId));
            }

            return id;
        }

        public FakeUser? Find(Guid id) => Users.FirstOrDefault(u => u.Id == id);

        public bool IsInRole(Guid userId, string roleName) => Memberships.Any(m => m.UserId == userId && Roles.GetValueOrDefault(m.RoleId) == roleName);

        // ---------- IIdentityAdministration ----------

        public Task<Result<IReadOnlyList<RoleSummary>>> ListRolesAsync(CancellationToken cancellationToken = default)
        {
            var list = Roles
                .Select(r => new RoleSummary(
                    r.Key,
                    r.Value,
                    r.Value == RoleNames.SuperAdmin
                        ? Permissions.AllCodes.Count
                        : _rolePermissions.Items.Count(rp => rp.RoleId == r.Key),
                    Memberships.Count(m => m.RoleId == r.Key),
                    RoleNames.All.Contains(r.Value)))
                .OrderBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(Result<IReadOnlyList<RoleSummary>>.Ok(list));
        }

        public Task<Result<RoleBasic>> FindRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                Roles.TryGetValue(roleId, out var name)
                    ? Result<RoleBasic>.Ok(new RoleBasic(roleId, name))
                    : Result<RoleBasic>.Fail(IdentityErrors.RoleNotFound));

        public Task<Result<RoleBasic>> FindRoleByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var match = Roles.FirstOrDefault(r => string.Equals(r.Value, name.Trim(), StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(
                match.Value is null
                    ? Result<RoleBasic>.Fail(IdentityErrors.RoleNotFound)
                    : Result<RoleBasic>.Ok(new RoleBasic(match.Key, match.Value)));
        }

        public Task<Result<int>> CountUsersInRoleAsync(Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(Result<int>.Ok(Memberships.Count(m => m.RoleId == roleId)));

        public Task<Result<int>> CountActiveUsersInRoleAsync(Guid roleId, Guid excludingUserId, CancellationToken cancellationToken = default)
        {
            var count = Memberships.Count(m =>
                m.RoleId == roleId
                && m.UserId != excludingUserId
                && Users.Any(u => u.Id == m.UserId && u.IsActive));

            return Task.FromResult(Result<int>.Ok(count));
        }

        public Task<Result<RoleBasic>> CreateRoleAsync(string? name)
        {
            var clean = name?.Trim();

            if (string.IsNullOrEmpty(clean))
            {
                return Task.FromResult(Result<RoleBasic>.Fail(IdentityErrors.RoleNameRequired));
            }

            if (Roles.Values.Any(r => string.Equals(r, clean, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(Result<RoleBasic>.Fail(IdentityErrors.RoleAlreadyExists));
            }

            var id = SeedRole(clean);
            return Task.FromResult(Result<RoleBasic>.Ok(new RoleBasic(id, clean)));
        }

        public Task<Result> DeleteRoleAsync(Guid roleId) => Task.FromResult(Roles.Remove(roleId) ? Result.Ok() : Result.Fail(IdentityErrors.RoleNotFound));

        public Task<Result<UserSummary>> CreateUserAsync(string? userName, string? email, string? phoneNumber, DateTimeOffset now)
        {
            var cleanName = userName?.Trim();
            var cleanEmail = email?.Trim();

            if (string.IsNullOrEmpty(cleanName))
            {
                return Task.FromResult(Result<UserSummary>.Fail(IdentityErrors.UserNameRequired));
            }

            if (!EmailAddressValidator.IsValid(cleanEmail) || cleanEmail is null)
            {
                return Task.FromResult(Result<UserSummary>.Fail(IdentityErrors.UserEmailInvalid));
            }

            if (Users.Any(u =>
                    string.Equals(u.UserName, cleanName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(u.Email, cleanEmail, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(Result<UserSummary>.Fail(IdentityErrors.UserAlreadyExists));
            }

            var user = new FakeUser
            {
                Id = Guid.NewGuid(),
                UserName = cleanName,
                Email = cleanEmail,
                IsActive = true,
                CreatedAt = now
            };
            Users.Add(user);

            return Task.FromResult(Result<UserSummary>.Ok(Summary(user)));
        }

        public Task<Result<UserDetail>> GetUserDetailAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = Find(userId);
            if (user is null)
            {
                return Task.FromResult(Result<UserDetail>.Fail(IdentityErrors.UserNotFound));
            }

            var scopes = _scopes.Items
                .Where(s => s.UserId == userId)
                .Select(s => new ScopeAssignmentInfo(s.Id, Roles.GetValueOrDefault(s.RoleId) ?? string.Empty, s.ScopeType, s.ScopeId, s.CreatedAt))
                .ToList();

            return Task.FromResult(Result<UserDetail>.Ok(new UserDetail(Summary(user), scopes)));
        }

        public Task<Result<PagedResult<UserSummary>>> ListUsersAsync(PaginationQuery query, string? search, bool? isActive, CancellationToken cancellationToken = default)
        {
            var users = Users.AsEnumerable();

            if (isActive is { } active)
            {
                users = users.Where(u => u.IsActive == active);
            }

            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(u =>
                    u.UserName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = users.OrderBy(u => u.UserName, StringComparer.Ordinal).ToList();

            var items = ordered.Skip(query.Skip).Take(query.NormalizedPageSize).Select(Summary).ToList();

            return Task.FromResult(Result<PagedResult<UserSummary>>.Ok(PagedResult<UserSummary>.Create(items, query, ordered.Count)));
        }

        public Task<Result> SetActiveAsync(Guid userId, bool active, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            var user = Find(userId);
            if (user is null)
            {
                return Task.FromResult(Result.Fail(IdentityErrors.UserNotFound));
            }

            user.IsActive = active;
            return Task.FromResult(Result.Ok());
        }

        public Task<Result<bool>> AddUserToRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                Find(userId) is null
                    ? Result<bool>.Fail(IdentityErrors.UserNotFound)
                    : Result<bool>.Ok(Memberships.Add((userId, roleId))));

        public Task<Result<bool>> RemoveUserFromRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default) => Task.FromResult(Result<bool>.Ok(Memberships.Remove((userId, roleId))));

        private UserSummary Summary(FakeUser user)
            => new(
                user.Id, user.UserName, user.Email, user.IsActive, user.CreatedAt, null,
                [.. Memberships.Where(m => m.UserId == user.Id)
                    .Select(m => Roles.GetValueOrDefault(m.RoleId) ?? string.Empty)
                    .Order(StringComparer.Ordinal)]);
    }

}
