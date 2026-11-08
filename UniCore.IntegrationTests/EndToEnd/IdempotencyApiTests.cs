using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using UniCore.Api.Filters;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Workflow;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    // The generic Idempotency-Key support, through the first endpoint that uses it: submitting a request (POST /workflows).
    [Collection(ApiCollection.Name)]
    public sealed class IdempotencyApiTests(ApiFixture fixture)
    {
        private readonly TestApi _api = fixture.Api;

        private ApiClient Client => _api.Client;

        private static string NewKey() => $"it-{Guid.NewGuid():N}";

        private static IReadOnlyDictionary<string, string> Key(string key) => new Dictionary<string, string> { [IdempotencyFilter.HeaderName] = key };

        private static object Body(Guid reference, Guid department)
            => new { definitionCode = "WITHDRAWAL_REQUEST", referenceType = "TestRequest", referenceId = reference, scopeType = "Department", scopeId = department };

        private async Task<string> StudentAsync()
        {
            var user = await _api.CreateUserAsync("idem", RoleNames.Student);

            return await _api.LoginTokenAsync(user.UserName, user.Password);
        }

        private Task<ApiResponse> StartAsync(string token, string? key, object body)
            => key is null
                ? Client.PostAsync("/api/v1/workflows", body, token)
                : Client.PostAsync("/api/v1/workflows", body, token, Key(key));

        private async Task<int> InstancesAsync(Guid reference)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await db.Set<WorkflowInstance>().CountAsync(i => i.ReferenceId == reference);
        }

        [Fact]
        public async Task The_header_is_required()
        {
            var student = await StudentAsync();

            var missing = await StartAsync(student, null, Body(Guid.NewGuid(), Guid.NewGuid()));

            Assert.Equal(400, missing.Code);
            Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", missing.ErrorCode);
        }

        [Theory]
        [InlineData("short")]
        [InlineData("has a space in it")]
        public async Task A_malformed_key_is_refused(string key)
        {
            var student = await StudentAsync();

            var response = await StartAsync(student, key, Body(Guid.NewGuid(), Guid.NewGuid()));

            Assert.Equal(400, response.Code);
            Assert.Equal("IDEMPOTENCY_KEY_INVALID", response.ErrorCode);
        }

        [Fact]
        public async Task A_retry_with_the_same_key_gets_the_same_response_and_creates_nothing_new()
        {
            var student = await StudentAsync();
            var reference = Guid.NewGuid();
            var body = Body(reference, Guid.NewGuid());
            var key = NewKey();

            var first = await StartAsync(student, key, body);
            var retry = await StartAsync(student, key, body);

            Assert.Equal(201, first.Code);
            Assert.Equal(201, retry.Code);
            Assert.Equal(first.Str("id"), retry.Str("id"));
            Assert.Equal(first.Raw, retry.Raw);
            Assert.Equal(1, await InstancesAsync(reference));
        }

        [Fact]
        public async Task A_replay_is_marked_and_keeps_the_location_header()
        {
            var student = await StudentAsync();
            var body = Body(Guid.NewGuid(), Guid.NewGuid());
            var key = NewKey();
            using var client = fixture.CreateHttpClient();

            async Task<HttpResponseMessage> SendAsync()
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/workflows") { Content = JsonContent.Create(body) };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", student);
                request.Headers.Add(IdempotencyFilter.HeaderName, key);

                return await client.SendAsync(request);
            }

            using var first = await SendAsync();
            using var replay = await SendAsync();

            Assert.Equal(201, (int)first.StatusCode);
            Assert.False(first.Headers.Contains(IdempotencyFilter.ReplayedHeaderName));

            Assert.Equal(201, (int)replay.StatusCode);
            Assert.True(replay.Headers.TryGetValues(IdempotencyFilter.ReplayedHeaderName, out var replayed));
            Assert.Equal("true", replayed.Single());
            Assert.NotNull(first.Headers.Location);
            Assert.Equal(first.Headers.Location, replay.Headers.Location);
            Assert.Equal("application/json", replay.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task The_same_key_for_a_different_request_is_a_conflict()
        {
            var student = await StudentAsync();
            var key = NewKey();
            var department = Guid.NewGuid();

            var first = await StartAsync(student, key, Body(Guid.NewGuid(), department));
            var other = await StartAsync(student, key, Body(Guid.NewGuid(), department));

            Assert.Equal(201, first.Code);
            Assert.Equal(409, other.Code);
            Assert.Equal("IDEMPOTENCY_KEY_REUSED", other.ErrorCode);
        }

        [Fact]
        public async Task Two_users_can_use_the_same_key()
        {
            var one = await StudentAsync();
            var two = await StudentAsync();
            var key = NewKey();
            var department = Guid.NewGuid();

            var first = await StartAsync(one, key, Body(Guid.NewGuid(), department));
            var second = await StartAsync(two, key, Body(Guid.NewGuid(), department));

            Assert.Equal(201, first.Code);
            Assert.Equal(201, second.Code);
            Assert.NotEqual(first.Str("id"), second.Str("id"));
        }

        [Fact]
        public async Task A_failed_request_is_not_stored_and_its_key_can_be_used_again()
        {
            var student = await StudentAsync();
            var key = NewKey();
            var invalid = new { definitionCode = "WITHDRAWAL_REQUEST", referenceType = " ", referenceId = Guid.NewGuid() };

            var failed = await StartAsync(student, key, invalid);
            var failedAgain = await StartAsync(student, key, invalid);

            Assert.Equal(400, failed.Code);
            Assert.Equal(400, failedAgain.Code);
            Assert.NotEqual("IDEMPOTENCY_KEY_REUSED", failedAgain.ErrorCode);

            // The failure freed the key: a corrected request may use it.
            var fixedRequest = await StartAsync(student, key, Body(Guid.NewGuid(), Guid.NewGuid()));

            Assert.Equal(201, fixedRequest.Code);
        }

        [Fact]
        public async Task Of_many_parallel_identical_requests_only_one_runs()
        {
            var student = await StudentAsync();
            var reference = Guid.NewGuid();
            var body = Body(reference, Guid.NewGuid());
            var key = NewKey();

            var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => StartAsync(student, key, body)));

            var created = responses.Where(r => r.Code == 201).ToList();

            Assert.NotEmpty(created);
            Assert.All(responses, r => Assert.True(r.Code is 201 or 409, $"Unexpected status {r.Code}"));
            Assert.All(responses.Where(r => r.Code == 409), r => Assert.Equal("IDEMPOTENCY_REQUEST_IN_PROGRESS", r.ErrorCode));
            Assert.Single(created.Select(r => r.Str("id")).Distinct());
            Assert.Equal(1, await InstancesAsync(reference));
        }

        [Fact]
        public async Task Without_a_token_the_request_is_refused_before_any_key_is_used()
        {
            var response = await Client.PostAsync("/api/v1/workflows", Body(Guid.NewGuid(), Guid.NewGuid()), null, Key(NewKey()));

            Assert.Equal(401, response.Code);
        }
    }
}
