using System.Text;
using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class RbacApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private async Task<Guid> RoleIdAsync(string name, string adminToken)
        {
            var roles = await Client.GetAsync("/api/v1/roles", adminToken);
            Assert.Equal(200, roles.Code);

            var match = roles.Body.EnumerateArray().First(r => r.GetProperty("name").GetString() == name);
            return Guid.Parse(match.GetProperty("id").GetString() ?? string.Empty);
        }

        [Fact]
        public async Task Admin_sees_the_whole_catalog_and_every_built_in_role()
        {
            var admin = await _api.AdminTokenAsync();

            var permissions = await Client.GetAsync("/api/v1/permissions", admin);
            Assert.Equal(200, permissions.Code);
            Assert.Equal(Permissions.All.Count, permissions.Body.GetArrayLength());

            var roles = await Client.GetAsync("/api/v1/roles", admin);
            Assert.Equal(200, roles.Code);

            var names = roles.Body.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToList();
            foreach (var name in RoleNames.All)
            {
                Assert.Contains(name, names);
            }

            var dean = roles.Body.EnumerateArray().First(r => r.GetProperty("name").GetString() == RoleNames.Dean);
            Assert.Equal(DefaultRolePermissions.ByRole[RoleNames.Dean].Count, dean.GetProperty("permissionCount").GetInt32());
            Assert.True(dean.GetProperty("isBuiltIn").GetBoolean());
        }

        [Fact]
        public async Task Creating_users_validates_input_and_rejects_duplicates()
        {
            var admin = await _api.AdminTokenAsync();
            var existing = await _api.CreateUserAsync("dup", setPassword: false);

            var duplicate = await Client.PostAsync(
                "/api/v1/users", new { userName = existing.UserName, email = $"{TestApi.NewName("x")}@uni.test" }, admin);
            Assert.Equal(409, duplicate.Code);
            Assert.Equal("USER_ALREADY_EXISTS", duplicate.ErrorCode);

            var badEmail = await Client.PostAsync(
                "/api/v1/users", new { userName = TestApi.NewName("u"), email = "not-an-email" }, admin);
            Assert.Equal(400, badEmail.Code);
            Assert.Equal("USER_EMAIL_INVALID", badEmail.ErrorCode);

            var noName = await Client.PostAsync("/api/v1/users", new { userName = " ", email = "a@uni.test" }, admin);
            Assert.Equal("USER_NAME_REQUIRED", noName.ErrorCode);

            var anonymous = await Client.PostAsync("/api/v1/users", new { userName = "x", email = "x@uni.test" });
            Assert.Equal(401, anonymous.Code);
        }

        [Fact]
        public async Task Listing_users_supports_search_and_needs_permission()
        {
            var admin = await _api.AdminTokenAsync();
            var user = await _api.CreateUserAsync("findme", RoleNames.Student);

            var found = await Client.GetAsync($"/api/v1/users?search={user.UserName}&pageSize=5", admin);
            Assert.Equal(200, found.Code);
            Assert.Equal(1, found.Prop("totalCount").GetInt32());
            Assert.Equal(user.UserName, found.Prop("items").EnumerateArray().First().GetProperty("userName").GetString());

            var asStudent = await Client.GetAsync("/api/v1/users", await _api.LoginTokenAsync(user.UserName, user.Password));
            Assert.Equal(403, asStudent.Code);
        }

        [Fact]
        public async Task A_dean_with_a_faculty_scope_is_limited_to_that_faculty()
        {
            var facultyA = Guid.NewGuid();
            var facultyB = Guid.NewGuid();
            var dean = await _api.CreateUserAsync("dean", RoleNames.Dean, ScopeType.Faculty, facultyA);
            var token = await _api.LoginTokenAsync(dean.UserName, dean.Password);

            var me = await Client.GetAsync("/api/v1/me", token);
            Assert.Equal(200, me.Code);
            Assert.Contains(RoleNames.Dean, me.Prop("roles").EnumerateArray().Select(e => e.GetString()));
            Assert.Equal(DefaultRolePermissions.ByRole[RoleNames.Dean].Count, me.Prop("permissions").GetArrayLength());

            var scope = Assert.Single(me.Prop("scopes").EnumerateArray());
            Assert.Equal("Faculty", scope.GetProperty("scopeType").GetString());
            Assert.Equal(facultyA.ToString(), scope.GetProperty("scopeId").GetString());

            var inScope = await Client.GetAsync(
                $"/api/v1/me/access-check?permission={Permissions.Grades.Approve}&scopeType=Faculty&scopeId={facultyA}", token);
            Assert.Equal(204, inScope.Code);

            var otherFaculty = await Client.GetAsync(
                $"/api/v1/me/access-check?permission={Permissions.Grades.Approve}&scopeType=Faculty&scopeId={facultyB}", token);
            Assert.Equal(403, otherFaculty.Code);
            Assert.Equal("FORBIDDEN_OUT_OF_SCOPE", otherFaculty.ErrorCode);

            var missingPermission = await Client.GetAsync(
                $"/api/v1/me/access-check?permission={Permissions.Settings.Manage}", token);
            Assert.Equal(403, missingPermission.Code);
            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", missingPermission.ErrorCode);

            var roles = await Client.GetAsync("/api/v1/roles", token);
            Assert.Equal(403, roles.Code);

            var createUser = await Client.PostAsync("/api/v1/users", new { userName = "x", email = "x@uni.test" }, token);
            Assert.Equal(403, createUser.Code);
            Assert.Equal("GENERAL_FORBIDDEN", createUser.ErrorCode);
        }

        [Fact]
        public async Task A_permission_that_is_not_in_the_catalog_is_denied_even_for_the_admin()
        {
            var admin = await _api.AdminTokenAsync();

            var response = await Client.GetAsync("/api/v1/me/access-check?permission=Nothing.Here", admin);

            Assert.Equal(403, response.Code);
            Assert.Equal("FORBIDDEN_PERMISSION_MISSING", response.ErrorCode);
        }

        [Fact]
        public async Task Role_permissions_take_effect_immediately_for_signed_in_users()
        {
            var admin = await _api.AdminTokenAsync();
            var roleName = TestApi.NewName("Role");

            var role = await Client.PostAsync("/api/v1/roles", new { name = roleName }, admin);
            Assert.Equal(201, role.Code);
            var roleId = role.GuidOf("id");

            var user = await _api.CreateUserAsync("custom", roleName);
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            var before = await Client.GetAsync("/api/v1/me/access-check?permission=Users.Read", token);
            Assert.Equal(403, before.Code);

            var granted = await Client.PutAsync(
                $"/api/v1/roles/{roleId}/permissions", new { permissions = new[] { Permissions.Users.Read } }, admin);
            Assert.Equal(200, granted.Code);

            // The same access token, no new login.
            var after = await Client.GetAsync("/api/v1/me/access-check?permission=Users.Read", token);
            Assert.Equal(204, after.Code);

            var me = await Client.GetAsync("/api/v1/me", token);
            var held = Assert.Single(me.Prop("permissions").EnumerateArray());
            Assert.Equal(Permissions.Users.Read, held.GetString());

            var revoked = await Client.PutAsync(
                $"/api/v1/roles/{roleId}/permissions", new { permissions = Array.Empty<string>() }, admin);
            Assert.Equal(200, revoked.Code);

            var gone = await Client.GetAsync("/api/v1/me/access-check?permission=Users.Read", token);
            Assert.Equal(403, gone.Code);

            // Clean up: a role that is still assigned cannot be deleted.
            var inUse = await Client.DeleteAsync($"/api/v1/roles/{roleId}", admin);
            Assert.Equal(409, inUse.Code);
            Assert.Equal("ROLE_IN_USE", inUse.ErrorCode);

            var removed = await Client.DeleteAsync($"/api/v1/users/{user.Id}/roles/{roleName}", admin);
            Assert.Equal(200, removed.Code);

            var deleted = await Client.DeleteAsync($"/api/v1/roles/{roleId}", admin);
            Assert.Equal(204, deleted.Code);
        }

        [Fact]
        public async Task Guard_rails_protect_the_admin_and_the_built_in_roles()
        {
            var admin = await _api.AdminTokenAsync();
            var me = await Client.GetAsync("/api/v1/me", admin);
            var adminId = me.GuidOf("userId");

            var ownRole = await Client.PostAsync($"/api/v1/users/{adminId}/roles", new { role = RoleNames.Student }, admin);
            Assert.Equal(403, ownRole.Code);
            Assert.Equal("CANNOT_MODIFY_OWN_ACCESS", ownRole.ErrorCode);

            var deactivateSelf = await Client.PostAsync($"/api/v1/users/{adminId}/deactivate", null, admin);
            Assert.Equal("CANNOT_MODIFY_OWN_ACCESS", deactivateSelf.ErrorCode);

            var removeSelf = await Client.DeleteAsync($"/api/v1/users/{adminId}/roles/{RoleNames.SuperAdmin}", admin);
            Assert.Equal("CANNOT_MODIFY_OWN_ACCESS", removeSelf.ErrorCode);

            var studentRoleId = await RoleIdAsync(RoleNames.Student, admin);
            var deleteBuiltIn = await Client.DeleteAsync($"/api/v1/roles/{studentRoleId}", admin);
            Assert.Equal(422, deleteBuiltIn.Code);
            Assert.Equal("ROLE_PROTECTED", deleteBuiltIn.ErrorCode);

            var superAdminRoleId = await RoleIdAsync(RoleNames.SuperAdmin, admin);
            var immutable = await Client.PutAsync(
                $"/api/v1/roles/{superAdminRoleId}/permissions", new { permissions = new[] { Permissions.Users.Read } }, admin);
            Assert.Equal(422, immutable.Code);
            Assert.Equal("ROLE_IMMUTABLE", immutable.ErrorCode);

            var unknownCode = await Client.PutAsync(
                $"/api/v1/roles/{studentRoleId}/permissions", new { permissions = new[] { "Nothing.Here" } }, admin);
            Assert.Equal(400, unknownCode.Code);
            Assert.Equal("PERMISSION_NOT_FOUND", unknownCode.ErrorCode);

            var missingList = await Client.PutAsync($"/api/v1/roles/{studentRoleId}/permissions", new { }, admin);
            Assert.Equal(400, missingList.Code);
            Assert.Equal("PERMISSIONS_REQUIRED", missingList.ErrorCode);

            var unknownRole = await Client.DeleteAsync($"/api/v1/roles/{Guid.NewGuid()}", admin);
            Assert.Equal(404, unknownRole.Code);
            Assert.Equal("ROLE_NOT_FOUND", unknownRole.ErrorCode);
        }

        [Fact]
        public async Task A_delegated_manager_cannot_hand_out_more_than_they_hold()
        {
            var admin = await _api.AdminTokenAsync();
            var managerRoleName = TestApi.NewName("Manager");

            var role = await Client.PostAsync("/api/v1/roles", new { name = managerRoleName }, admin);
            var managerRoleId = role.GuidOf("id");
            var grant = await Client.PutAsync(
                $"/api/v1/roles/{managerRoleId}/permissions", new { permissions = new[] { Permissions.Roles.Manage } }, admin);
            Assert.Equal(200, grant.Code);

            var manager = await _api.CreateUserAsync("mgr", managerRoleName);
            var target = await _api.CreateUserAsync("target", setPassword: false);
            var token = await _api.LoginTokenAsync(manager.UserName, manager.Password);

            var dean = await Client.PostAsync(
                $"/api/v1/users/{target.Id}/roles",
                new { role = RoleNames.Dean, scopeType = ScopeType.Faculty, scopeId = Guid.NewGuid() }, token);
            Assert.Equal(403, dean.Code);
            Assert.Equal("CANNOT_GRANT_PERMISSION_YOU_LACK", dean.ErrorCode);

            var superAdmin = await Client.PostAsync($"/api/v1/users/{target.Id}/roles", new { role = RoleNames.SuperAdmin }, token);
            Assert.Equal("ONLY_SUPERADMIN_CAN_MANAGE_SUPERADMIN", superAdmin.ErrorCode);

            var global = await Client.PostAsync(
                $"/api/v1/users/{target.Id}/roles", new { role = managerRoleName, scopeType = ScopeType.Global }, token);
            Assert.Equal("ONLY_SUPERADMIN_CAN_GRANT_GLOBAL_SCOPE", global.ErrorCode);

            var changePermissions = await Client.PutAsync(
                $"/api/v1/roles/{managerRoleId}/permissions", new { permissions = new[] { Permissions.Users.Read } }, token);
            Assert.Equal(403, changePermissions.Code);

            // A role the manager fully holds can be handed out.
            var allowed = await Client.PostAsync($"/api/v1/users/{target.Id}/roles", new { role = managerRoleName }, token);
            Assert.Equal(200, allowed.Code);

            // Clean up.
            await Client.DeleteAsync($"/api/v1/users/{target.Id}/roles/{managerRoleName}", admin);
            await Client.DeleteAsync($"/api/v1/users/{manager.Id}/roles/{managerRoleName}", admin);
            var deleted = await Client.DeleteAsync($"/api/v1/roles/{managerRoleId}", admin);
            Assert.Equal(204, deleted.Code);
        }

        [Fact]
        public async Task Deactivating_a_user_kills_their_sessions_and_their_access()
        {
            var admin = await _api.AdminTokenAsync();
            var user = await _api.CreateUserAsync("off", RoleNames.Student);
            var session = await _api.LoginAsync(user.UserName, user.Password);
            Assert.Equal(200, session.Code);

            var beforeMe = await Client.GetAsync("/api/v1/me", session.Str("accessToken"));
            Assert.True(beforeMe.Prop("permissions").GetArrayLength() > 0);

            var deactivated = await Client.PostAsync($"/api/v1/users/{user.Id}/deactivate", null, admin);
            Assert.Equal(200, deactivated.Code);
            Assert.False(deactivated.Prop("isActive").GetBoolean());

            var refresh = await Client.PostAsync("/api/v1/auth/refresh", new { refreshToken = session.Str("refreshToken") });
            Assert.Equal(401, refresh.Code);

            var login = await _api.LoginAsync(user.UserName, user.Password);
            Assert.Equal(401, login.Code);
            Assert.Equal("INVALID_CREDENTIALS", login.ErrorCode);

            // The old access token still authenticates, but it carries no roles or permissions any more.
            var afterMe = await Client.GetAsync("/api/v1/me", session.Str("accessToken"));
            Assert.Equal(200, afterMe.Code);
            Assert.Equal(0, afterMe.Prop("permissions").GetArrayLength());
            Assert.Equal(0, afterMe.Prop("roles").GetArrayLength());

            var activated = await Client.PostAsync($"/api/v1/users/{user.Id}/activate", null, admin);
            Assert.Equal(200, activated.Code);

            var again = await _api.LoginAsync(user.UserName, user.Password);
            Assert.Equal(200, again.Code);
        }
    }

}
