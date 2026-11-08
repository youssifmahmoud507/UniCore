using UniCore.Domain.Modules.Identity;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The user's access and the document types are cached. A change must be visible on the very next request,
    // not when the cache entry expires.
    [Collection(ApiCollection.Name)]
    public sealed class CacheInvalidationApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        [Fact]
        public async Task Removing_and_assigning_a_role_is_seen_at_once_even_when_the_access_is_cached()
        {
            var admin = await _api.AdminTokenAsync();
            var roleName = TestApi.NewName("CacheRole");
            var role = await Client.PostAsync("/api/v1/roles", new { name = roleName }, admin);
            Assert.Equal(201, role.Code);
            var roleId = role.GuidOf("id");
            Assert.Equal(200, (await Client.PutAsync($"/api/v1/roles/{roleId}/permissions", new { permissions = new[] { Permissions.Users.Read } }, admin)).Code);

            var user = await _api.CreateUserAsync("cacheuser", roleName);
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);
            const string Check = "/api/v1/me/access-check?permission=Users.Read";

            // Twice: the second answer comes from the cache.
            Assert.Equal(204, (await Client.GetAsync(Check, token)).Code);
            Assert.Equal(204, (await Client.GetAsync(Check, token)).Code);

            Assert.Equal(200, (await Client.DeleteAsync($"/api/v1/users/{user.Id}/roles/{roleName}", admin)).Code);
            Assert.Equal(403, (await Client.GetAsync(Check, token)).Code);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/users/{user.Id}/roles", new { role = roleName }, admin)).Code);
            Assert.Equal(204, (await Client.GetAsync(Check, token)).Code);

            // Clean up: a role that is still assigned cannot be deleted.
            Assert.Equal(200, (await Client.DeleteAsync($"/api/v1/users/{user.Id}/roles/{roleName}", admin)).Code);
            Assert.Equal(204, (await Client.DeleteAsync($"/api/v1/roles/{roleId}", admin)).Code);
        }

        [Fact]
        public async Task Changing_the_permissions_of_a_role_is_seen_at_once_for_every_user_of_the_role()
        {
            var admin = await _api.AdminTokenAsync();
            var roleName = TestApi.NewName("CacheRole");
            var role = await Client.PostAsync("/api/v1/roles", new { name = roleName }, admin);
            var roleId = role.GuidOf("id");
            var first = await _api.CreateUserAsync("cachea", roleName);
            var second = await _api.CreateUserAsync("cacheb", roleName);
            var firstToken = await _api.LoginTokenAsync(first.UserName, first.Password);
            var secondToken = await _api.LoginTokenAsync(second.UserName, second.Password);
            const string Check = "/api/v1/me/access-check?permission=Users.Read";

            Assert.Equal(403, (await Client.GetAsync(Check, firstToken)).Code);
            Assert.Equal(403, (await Client.GetAsync(Check, secondToken)).Code);

            await Client.PutAsync($"/api/v1/roles/{roleId}/permissions", new { permissions = new[] { Permissions.Users.Read } }, admin);

            Assert.Equal(204, (await Client.GetAsync(Check, firstToken)).Code);
            Assert.Equal(204, (await Client.GetAsync(Check, secondToken)).Code);

            await Client.PutAsync($"/api/v1/roles/{roleId}/permissions", new { permissions = Array.Empty<string>() }, admin);

            Assert.Equal(403, (await Client.GetAsync(Check, firstToken)).Code);
            Assert.Equal(403, (await Client.GetAsync(Check, secondToken)).Code);

            await Client.DeleteAsync($"/api/v1/users/{first.Id}/roles/{roleName}", admin);
            await Client.DeleteAsync($"/api/v1/users/{second.Id}/roles/{roleName}", admin);
            await Client.DeleteAsync($"/api/v1/roles/{roleId}", admin);
        }

        [Fact]
        public async Task Deactivating_and_activating_a_user_is_seen_at_once_even_when_the_access_is_cached()
        {
            var admin = await _api.AdminTokenAsync();
            var user = await _api.CreateUserAsync("cachestate", RoleNames.Student);
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            Assert.True((await Client.GetAsync("/api/v1/me", token)).Prop("permissions").GetArrayLength() > 0);
            Assert.True((await Client.GetAsync("/api/v1/me", token)).Prop("permissions").GetArrayLength() > 0);

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/users/{user.Id}/deactivate", null, admin)).Code);
            Assert.Equal(0, (await Client.GetAsync("/api/v1/me", token)).Prop("permissions").GetArrayLength());

            Assert.Equal(200, (await Client.PostAsync($"/api/v1/users/{user.Id}/activate", null, admin)).Code);
            Assert.True((await Client.GetAsync("/api/v1/me", token)).Prop("permissions").GetArrayLength() > 0);
        }

        [Fact]
        public async Task A_new_document_type_shows_up_in_the_cached_list_at_once()
        {
            var admin = await _api.AdminTokenAsync();
            var user = await _api.CreateUserAsync("cachetypes", RoleNames.Student);
            var token = await _api.LoginTokenAsync(user.UserName, user.Password);

            static List<string?> Codes(ApiResponse list)
                => [.. Enumerable.Range(0, list.Body.GetArrayLength()).Select(i => list.Body[i].GetProperty("code").GetString())];

            var warm = await Client.GetAsync("/api/v1/document-types", token);
            var warmAgain = await Client.GetAsync("/api/v1/document-types", token);
            Assert.Equal(200, warm.Code);
            Assert.Equal(Codes(warm), Codes(warmAgain));

            var code = $"C{Guid.NewGuid():N}"[..12].ToUpperInvariant();
            var created = await Client.PostAsync("/api/v1/document-types", new { code, name = "Cache test", isSensitive = false, allowedFormats = new[] { "Pdf" } }, admin);
            Assert.Equal(201, created.Code);

            var after = await Client.GetAsync("/api/v1/document-types", token);

            Assert.Contains(code, Codes(after));
            Assert.Equal(Codes(warm).Count + 1, Codes(after).Count);
        }
    }
}
