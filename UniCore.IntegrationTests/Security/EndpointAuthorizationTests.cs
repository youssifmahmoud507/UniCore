using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Security
{
    // The runtime side of the Week 23 review: every endpoint the running API exposes.
    [Collection(ApiCollection.Name)]
    public sealed class EndpointAuthorizationTests(ApiFixture fixture)
    {
        // Not behind a login on purpose: probes, and the Development-only documentation.
        private static readonly string[] OpenPrefixes = ["/health/", "/openapi", "/scalar"];

        private static readonly HashSet<string> ApprovedAnonymousRoutes =
        [
            "/api/v1/auth/login",
            "/api/v1/auth/refresh",
            "/api/v1/auth/forgot-password",
            "/api/v1/auth/verify-otp",
            "/api/v1/auth/reset-password"
        ];

        private static string PathOf(RouteEndpoint endpoint) => "/" + (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');

        private static bool IsOpen(RouteEndpoint endpoint) =>
            OpenPrefixes.Any(prefix => PathOf(endpoint).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        private List<RouteEndpoint> Endpoints() =>
            [.. fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

        [Fact]
        public void The_running_api_exposes_endpoints()
            => Assert.True(Endpoints().Count > 40);

        [Fact]
        public void Every_endpoint_requires_authentication_unless_it_is_on_the_open_list()
        {
            var unprotected = Endpoints()
                .Where(e => !IsOpen(e) && !e.Metadata.OfType<IAuthorizeData>().Any() && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
                .Select(PathOf)
                .Distinct()
                .ToList();

            Assert.True(unprotected.Count == 0, "Endpoints without authorization: " + string.Join(", ", unprotected));
        }

        [Fact]
        public void Only_the_auth_endpoints_allow_anonymous_access()
        {
            var anonymous = Endpoints()
                .Where(e => !IsOpen(e) && e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                .Select(PathOf)
                .ToHashSet();

            Assert.True(anonymous.SetEquals(ApprovedAnonymousRoutes),
                "Anonymous endpoints changed. Unexpected: [" + string.Join(", ", anonymous.Except(ApprovedAnonymousRoutes)) +
                "] Missing: [" + string.Join(", ", ApprovedAnonymousRoutes.Except(anonymous)) + "]");
        }

        [Theory]
        [InlineData("/api/v1/roles")]
        [InlineData("/api/v1/users")]
        [InlineData("/api/v1/students")]
        [InlineData("/api/v1/documents")]
        public async Task Calls_without_a_token_get_401(string path)
        {
            using var client = fixture.CreateHttpClient();

            using var response = await client.GetAsync(path);

            Assert.Equal(401, (int)response.StatusCode);
        }

        [Fact]
        public async Task The_notifications_hub_refuses_a_connection_without_a_token()
        {
            using var client = fixture.CreateHttpClient();

            using var response = await client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", content: null);

            Assert.Equal(401, (int)response.StatusCode);
        }
    }
}
