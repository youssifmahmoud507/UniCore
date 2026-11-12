using UniCore.Domain.Modules.Identity;

namespace UniCore.IntegrationTests.Infrastructure
{
    // A signed-in user whose role holds exactly the given permissions (an own role, so no default role can change the
    // result), optionally in one scope.
    public static class RoleUsers
    {
        public static async Task<string> TokenAsync(TestApi api, string prefix, IEnumerable<string> permissions, ScopeType? scopeType = null, Guid? scopeId = null)
        {
            var admin = await api.AdminTokenAsync();
            var roleName = TestApi.NewName(prefix + "Role");

            var role = await api.Client.PostAsync("/api/v1/roles", new { name = roleName }, admin);
            Assert.Equal(201, role.Code);

            var granted = await api.Client.PutAsync($"/api/v1/roles/{role.GuidOf("id")}/permissions", new { permissions = permissions.ToArray() }, admin);
            Assert.Equal(200, granted.Code);

            var user = await api.CreateUserAsync(prefix, roleName, scopeType, scopeId);

            return await api.LoginTokenAsync(user.UserName, user.Password);
        }
    }

}
