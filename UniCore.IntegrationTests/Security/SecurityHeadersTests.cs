using System.Net.Http.Json;
using UniCore.Api.Security;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Security
{
    [Collection(ApiCollection.Name)]
    public sealed class SecurityHeadersTests(ApiFixture fixture)
    {
        // From appsettings.Development.json.
        private const string AllowedOrigin = "http://localhost:5173";

        private static string? Header(HttpResponseMessage response, string name) =>
            response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

        private static void AssertSecurityHeaders(HttpResponseMessage response)
        {
            Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
            Assert.Equal("DENY", Header(response, "X-Frame-Options"));
            Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
            Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicyValue, Header(response, "Content-Security-Policy"));
        }

        private static HttpRequestMessage Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
            return request;
        }

        [Fact]
        public async Task Normal_responses_have_the_security_headers()
        {
            using var client = fixture.CreateHttpClient();

            using var response = await client.GetAsync("/health/live");

            Assert.True(response.IsSuccessStatusCode);
            AssertSecurityHeaders(response);
        }

        [Fact]
        public async Task Rejected_requests_have_the_security_headers_too()
        {
            using var client = fixture.CreateHttpClient();

            using var response = await client.GetAsync("/api/v1/roles");

            Assert.Equal(401, (int)response.StatusCode);
            AssertSecurityHeaders(response);
        }

        [Fact]
        public async Task Auth_responses_are_not_cacheable()
        {
            using var client = fixture.CreateHttpClient();

            using var response = await client.PostAsync("/api/v1/auth/login", JsonContent.Create(new { login = "nobody", password = "wrong-password" }));

            Assert.Equal("no-store", Header(response, "Cache-Control"));
        }

        [Fact]
        public async Task Preflight_from_a_configured_origin_is_accepted()
        {
            using var client = fixture.CreateHttpClient();
            using var request = Preflight(AllowedOrigin);

            using var response = await client.SendAsync(request);

            Assert.Equal(AllowedOrigin, Header(response, "Access-Control-Allow-Origin"));
            Assert.Contains("POST", Header(response, "Access-Control-Allow-Methods") ?? string.Empty);
            Assert.Null(Header(response, "Access-Control-Allow-Credentials"));
        }

        [Fact]
        public async Task Preflight_from_any_other_origin_gets_no_allow_header()
        {
            using var client = fixture.CreateHttpClient();
            using var request = Preflight("https://evil.example.com");

            using var response = await client.SendAsync(request);

            Assert.Null(Header(response, "Access-Control-Allow-Origin"));
        }

        [Fact]
        public async Task Hangfire_dashboard_is_not_exposed_unless_enabled()
        {
            using var client = fixture.CreateHttpClient();

            using var response = await client.GetAsync("/hangfire");

            Assert.Equal(404, (int)response.StatusCode);
        }
    }
}
