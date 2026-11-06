using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using UniCore.Api.Extensions;
using UniCore.Application.Modules.Identity;

namespace UniCore.IntegrationTests.Security
{
    public class RateLimitingTests
    {
        private static async Task<WebApplication> StartAsync(int general = 100, int upload = 20)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
            builder.WebHost.UseTestServer();
            builder.Configuration["RateLimiting:GeneralPerMinute"] = general.ToString(CultureInfo.InvariantCulture);
            builder.Configuration["RateLimiting:UploadPerMinute"] = upload.ToString(CultureInfo.InvariantCulture);
            builder.Services.AddApiRateLimiting();

            var app = builder.Build();

            // Stands in for the JWT: the header X-Test-User signs that user in.
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-User", out var user))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AppClaimTypes.Sub, user.ToString())], "test"));
                }

                await next();
            });
            app.UseRateLimiter();

            app.MapGet("/api/v1/ping", () => Results.Ok());
            app.MapPost("/api/v1/auth/login", () => Results.Ok()).RequireRateLimiting(RateLimitPolicies.Login);
            app.MapPost("/api/v1/documents", () => Results.Ok()).RequireRateLimiting(RateLimitPolicies.Upload);
            app.MapGet("/health/live", () => Results.Ok());

            await app.StartAsync();
            return app;
        }

        private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? user = null)
        {
            using var request = new HttpRequestMessage(method, path);

            if (user is not null)
            {
                request.Headers.Add("X-Test-User", user);
            }

            return await client.SendAsync(request);
        }

        private static async Task<int[]> StatusesAsync(HttpClient client, HttpMethod method, string path, int count, string? user = null)
        {
            var codes = new List<int>();

            for (var i = 0; i < count; i++)
            {
                using var response = await SendAsync(client, method, path, user);
                codes.Add((int)response.StatusCode);
            }

            return [.. codes];
        }

        [Fact]
        public async Task Login_allows_five_attempts_a_minute_then_returns_429()
        {
            await using var app = await StartAsync();
            var client = app.GetTestClient();

            var codes = await StatusesAsync(client, HttpMethod.Post, "/api/v1/auth/login", 6);

            Assert.Equal([200, 200, 200, 200, 200, 429], codes);
        }

        [Fact]
        public async Task Rejection_is_a_problem_response_with_retry_after_and_no_account_details()
        {
            await using var app = await StartAsync();
            var client = app.GetTestClient();
            await StatusesAsync(client, HttpMethod.Post, "/api/v1/auth/login", 5);

            using var response = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login");
            var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

            Assert.Equal(429, (int)response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Retry-After", out var retryAfter) && int.Parse(retryAfter.First(), CultureInfo.InvariantCulture) > 0);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("RATE_LIMIT_EXCEEDED", body.GetProperty("errorCode").GetString());
        }

        [Fact]
        public async Task General_limit_is_counted_per_user()
        {
            await using var app = await StartAsync(general: 3);
            var client = app.GetTestClient();

            var alice = await StatusesAsync(client, HttpMethod.Get, "/api/v1/ping", 4, "alice");
            var bob = await StatusesAsync(client, HttpMethod.Get, "/api/v1/ping", 1, "bob");

            Assert.Equal([200, 200, 200, 429], alice);
            Assert.Equal([200], bob);
        }

        [Fact]
        public async Task Anonymous_callers_are_limited_too()
        {
            await using var app = await StartAsync(general: 2);
            var client = app.GetTestClient();

            var codes = await StatusesAsync(client, HttpMethod.Get, "/api/v1/ping", 3);

            Assert.Equal([200, 200, 429], codes);
        }

        [Fact]
        public async Task Health_checks_are_never_limited()
        {
            await using var app = await StartAsync(general: 1);
            var client = app.GetTestClient();

            var codes = await StatusesAsync(client, HttpMethod.Get, "/health/live", 10);

            Assert.All(codes, code => Assert.Equal(200, code));
        }

        [Fact]
        public async Task Upload_limit_is_stricter_and_per_user()
        {
            await using var app = await StartAsync(general: 100, upload: 2);
            var client = app.GetTestClient();

            var carol = await StatusesAsync(client, HttpMethod.Post, "/api/v1/documents", 3, "carol");
            var dave = await StatusesAsync(client, HttpMethod.Post, "/api/v1/documents", 1, "dave");

            Assert.Equal([200, 200, 429], carol);
            Assert.Equal([200], dave);
        }

        [Theory]
        [InlineData(100, 20, true)]
        [InlineData(1, 1, true)]
        [InlineData(0, 20, false)]
        [InlineData(100, 0, false)]
        [InlineData(100_001, 20, false)]
        public void Options_must_be_in_range(int general, int upload, bool valid)
            => Assert.Equal(valid, RateLimitingOptions.IsValid(new RateLimitingOptions { GeneralPerMinute = general, UploadPerMinute = upload }));
    }
}
