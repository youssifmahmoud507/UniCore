using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Idempotency;
using UniCore.Domain.Modules.Idempotency;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Idempotency
{
    [Collection(ApiCollection.Name)]
    public sealed class IdempotencyServiceTests(ApiFixture fixture)
    {
        private static readonly string HashA = new('a', IdempotencyRecord.HashLength);
        private static readonly string HashB = new('b', IdempotencyRecord.HashLength);

        private static string NewKey() => $"svc-{Guid.NewGuid():N}";

        private static StoredResponse Response() => new(201, "application/json", "/api/v1/things/1", Encoding.UTF8.GetBytes("{\"id\":1}"));

        // Each call gets its own scope (and DbContext), like separate HTTP requests.
        private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await action(scope.ServiceProvider);
        }

        private async Task<IdempotencyBegin> BeginAsync(string key, Guid userId, string hash)
        {
            var result = await InScopeAsync(services => services.GetRequiredService<IIdempotencyService>().BeginAsync(key, userId, hash));

            Assert.True(result.TryGetValue(out var begin));
            Assert.NotNull(begin);
            return begin;
        }

        [Fact]
        public async Task A_new_key_is_started()
        {
            var begin = await BeginAsync(NewKey(), Guid.NewGuid(), HashA);

            Assert.Equal(IdempotencyOutcome.Started, begin.Outcome);
            Assert.NotEqual(Guid.Empty, begin.RecordId);
            Assert.Null(begin.Response);
        }

        [Fact]
        public async Task The_same_key_while_the_first_request_runs_is_in_progress()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            var first = await BeginAsync(key, user, HashA);

            var second = await BeginAsync(key, user, HashA);

            Assert.Equal(IdempotencyOutcome.InProgress, second.Outcome);
            Assert.Equal(first.RecordId, second.RecordId);
        }

        [Fact]
        public async Task A_completed_request_is_replayed_with_its_stored_response()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            var first = await BeginAsync(key, user, HashA);

            var completed = await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().CompleteAsync(first.RecordId, Response()));
            Assert.True(completed.IsSuccess);

            var replay = await BeginAsync(key, user, HashA);

            Assert.Equal(IdempotencyOutcome.Replay, replay.Outcome);
            Assert.NotNull(replay.Response);
            Assert.Equal(201, replay.Response.StatusCode);
            Assert.Equal("application/json", replay.Response.ContentType);
            Assert.Equal("/api/v1/things/1", replay.Response.Location);
            Assert.Equal("{\"id\":1}", Encoding.UTF8.GetString(replay.Response.Body));
        }

        [Fact]
        public async Task The_same_key_for_another_request_is_a_mismatch_whether_running_or_done()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            var first = await BeginAsync(key, user, HashA);

            Assert.Equal(IdempotencyOutcome.Mismatch, (await BeginAsync(key, user, HashB)).Outcome);

            await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().CompleteAsync(first.RecordId, Response()));

            Assert.Equal(IdempotencyOutcome.Mismatch, (await BeginAsync(key, user, HashB)).Outcome);
        }

        [Fact]
        public async Task Keys_are_separate_for_each_user()
        {
            var key = NewKey();

            var one = await BeginAsync(key, Guid.NewGuid(), HashA);
            var two = await BeginAsync(key, Guid.NewGuid(), HashA);

            Assert.Equal(IdempotencyOutcome.Started, one.Outcome);
            Assert.Equal(IdempotencyOutcome.Started, two.Outcome);
            Assert.NotEqual(one.RecordId, two.RecordId);
        }

        [Fact]
        public async Task Abandoning_a_running_request_frees_its_key()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            var first = await BeginAsync(key, user, HashA);

            var abandoned = await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().AbandonAsync(first.RecordId));
            var again = await BeginAsync(key, user, HashB);

            Assert.True(abandoned.IsSuccess);
            Assert.Equal(IdempotencyOutcome.Started, again.Outcome);
        }

        [Fact]
        public async Task Abandoning_never_removes_a_completed_record()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            var first = await BeginAsync(key, user, HashA);
            await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().CompleteAsync(first.RecordId, Response()));

            await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().AbandonAsync(first.RecordId));

            Assert.Equal(IdempotencyOutcome.Replay, (await BeginAsync(key, user, HashA)).Outcome);
        }

        [Fact]
        public async Task Completing_an_unknown_record_is_not_found()
        {
            var result = await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().CompleteAsync(Guid.NewGuid(), Response()));

            Assert.True(result.IsFailure);
            Assert.Equal("IDEMPOTENCY_RECORD_NOT_FOUND", result.Errors[0].Code);
        }

        [Fact]
        public async Task A_bad_key_or_hash_is_refused()
        {
            var badKey = await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().BeginAsync("short", Guid.NewGuid(), HashA));
            var badHash = await InScopeAsync(s => s.GetRequiredService<IIdempotencyService>().BeginAsync(NewKey(), Guid.NewGuid(), "not-a-hash"));

            Assert.Equal("IDEMPOTENCY_KEY_INVALID", badKey.Errors[0].Code);
            Assert.Equal("IDEMPOTENCY_RECORD_INVALID", badHash.Errors[0].Code);
        }

        [Fact]
        public async Task An_expired_key_can_be_used_again()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            var first = await BeginAsync(key, user, HashA);

            await InScopeAsync(async services =>
            {
                var now = services.GetRequiredService<IClock>().UtcNow;
                var db = services.GetRequiredService<AppDbContext>();
                return await db.Set<IdempotencyRecord>().Where(r => r.Id == first.RecordId)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.ExpiresAt, now.AddHours(-1)));
            });

            var again = await BeginAsync(key, user, HashB);

            Assert.Equal(IdempotencyOutcome.Started, again.Outcome);
            Assert.NotEqual(first.RecordId, again.RecordId);
        }

        [Fact]
        public async Task A_request_that_crashed_is_stale_and_its_key_can_be_used_again()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            var first = await BeginAsync(key, user, HashA);

            await InScopeAsync(async services =>
            {
                var now = services.GetRequiredService<IClock>().UtcNow;
                var db = services.GetRequiredService<AppDbContext>();
                return await db.Set<IdempotencyRecord>().Where(r => r.Id == first.RecordId)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, now.AddHours(-1)));
            });

            var again = await BeginAsync(key, user, HashA);

            Assert.Equal(IdempotencyOutcome.Started, again.Outcome);
            Assert.NotEqual(first.RecordId, again.RecordId);
        }

        [Fact]
        public async Task A_lost_claim_does_not_leave_a_duplicate_to_be_saved_later_on_the_same_context()
        {
            var key = NewKey();
            var user = Guid.NewGuid();
            await BeginAsync(key, user, HashA);

            // The handler of a request saves on the same DbContext the claim used: that save must still work.
            var outcome = await InScopeAsync(async services =>
            {
                var begun = await services.GetRequiredService<IIdempotencyService>().BeginAsync(key, user, HashA);
                var saved = await services.GetRequiredService<AppDbContext>().SaveChangesAsync();

                return (begun, saved);
            });

            Assert.Equal(IdempotencyOutcome.InProgress, outcome.begun.Value!.Outcome);
            Assert.Equal(0, outcome.saved);
        }

        [Fact]
        public async Task Of_many_parallel_claims_exactly_one_starts()
        {
            var key = NewKey();
            var user = Guid.NewGuid();

            var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => BeginAsync(key, user, HashA)));

            Assert.Equal(1, claims.Count(c => c.Outcome == IdempotencyOutcome.Started));
            Assert.Equal(7, claims.Count(c => c.Outcome == IdempotencyOutcome.InProgress));
        }
    }
}
