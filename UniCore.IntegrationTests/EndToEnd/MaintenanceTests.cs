using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Modules.Idempotency;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Modules.Maintenance;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.EndToEnd
{
    [Collection(ApiCollection.Name)]
    public sealed class MaintenanceTests(ApiFixture fixture)
    {
        private static OutboxMessage Message(DateTimeOffset createdAt, DateTimeOffset? notBefore = null)
        {
            var created = OutboxMessage.Create("test.maintenance", "{\"a\":1}", createdAt, createdAt, notBefore);
            Assert.True(created.TryGetValue(out var message));
            Assert.NotNull(message);
            return message;
        }

        [Fact]
        public async Task Cleanup_deletes_old_finished_outbox_messages_and_keeps_everything_else()
        {
            var now = DateTimeOffset.UtcNow;

            var oldProcessed = Message(now.AddDays(-100));
            oldProcessed.MarkProcessed(now.AddDays(-99), false);

            var recentProcessed = Message(now.AddDays(-1));
            recentProcessed.MarkProcessed(now.AddDays(-1), false);

            var oldPoison = Message(now.AddDays(-100));
            oldPoison.RecordFailure("X", true, now.AddDays(-99), false);

            var recentPoison = Message(now.AddDays(-5));
            recentPoison.RecordFailure("X", true, now.AddDays(-5), false);

            // Scheduled for tomorrow, so the API's own processor leaves it alone. Pending is never deleted.
            var oldPending = Message(now.AddDays(-100), notBefore: now.AddDays(1));

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Set<OutboxMessage>().AddRange(oldProcessed, recentProcessed, oldPoison, recentPoison, oldPending);
                await db.SaveChangesAsync();
            }

            CleanupResult? result;

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var outcome = await scope.ServiceProvider.GetRequiredService<DataCleanupService>().CleanAsync();

                Assert.True(outcome.TryGetValue(out result));
            }

            Assert.NotNull(result);
            Assert.Equal(0, result.Failed);
            Assert.True(result.OutboxMessages >= 2);

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = new[] { oldProcessed.Id, recentProcessed.Id, oldPoison.Id, recentPoison.Id, oldPending.Id };
                var left = await db.Set<OutboxMessage>().AsNoTracking().Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync();

                Assert.DoesNotContain(oldProcessed.Id, left);
                Assert.DoesNotContain(oldPoison.Id, left);
                Assert.Contains(recentProcessed.Id, left);
                Assert.Contains(recentPoison.Id, left);
                Assert.Contains(oldPending.Id, left);
            }
        }

        [Fact]
        public async Task The_expiry_queries_run_against_the_database_and_nothing_fresh_is_cancelled()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ExpiredRequestsService>();

            // Nothing in a test database has been idle for ten years.
            var stale = await service.FindStaleAsync(3650);
            Assert.True(stale.IsSuccess);
            Assert.Empty(stale.Value ?? []);

            var unknown = await service.ExpireAsync(Guid.NewGuid(), 60);
            Assert.True(unknown.IsSuccess);
            Assert.False(unknown.Value);
        }

        [Fact]
        public async Task The_runner_finishes_without_failures_and_the_jobs_are_registered()
        {
            var runner = fixture.Services.GetRequiredService<ExpiredRequestsRunner>();

            var result = await runner.RunAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value?.Failed);

            var storage = fixture.Services.GetRequiredService<Hangfire.JobStorage>();
            using var connection = storage.GetConnection();
            var jobs = Hangfire.Storage.StorageConnectionExtensions.GetRecurringJobs(connection);

            Assert.Contains(jobs, j => j.Id == ExpiredRequestsJob.RecurringJobId && j.Cron == "30 3 * * *");
            Assert.Contains(jobs, j => j.Id == DataCleanupJob.RecurringJobId && j.Cron == "0 4 * * *");
        }

        [Fact]
        public void The_options_have_sane_defaults_and_reject_nonsense()
        {
            Assert.True(MaintenanceOptions.IsValid(new MaintenanceOptions()));
            Assert.False(MaintenanceOptions.IsValid(new MaintenanceOptions { ExpiredRequestDays = 0 }));
            Assert.False(MaintenanceOptions.IsValid(new MaintenanceOptions { ProcessedOutboxRetentionDays = 5000 }));
            Assert.Equal(60, new MaintenanceOptions().ExpiredRequestDays);
        }


        [Fact]
        public async Task Cleanup_deletes_expired_idempotency_records_and_keeps_current_ones()
        {
            var now = DateTimeOffset.UtcNow;
            var hash = new string('a', IdempotencyRecord.HashLength);

            static IdempotencyRecord Record(string prefix, DateTimeOffset created, string hash)
            {
                var begun = IdempotencyRecord.Begin($"{prefix}-{Guid.NewGuid():N}", Guid.NewGuid(), hash, created, TimeSpan.FromHours(24));
                Assert.True(begun.TryGetValue(out var record));
                Assert.NotNull(record);
                return record;
            }

            var expired = Record("expired", now.AddDays(-3), hash);
            var current = Record("current", now, hash);

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Set<IdempotencyRecord>().AddRange(expired, current);
                await db.SaveChangesAsync();
            }

            CleanupResult? result;

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var outcome = await scope.ServiceProvider.GetRequiredService<DataCleanupService>().CleanAsync();

                Assert.True(outcome.TryGetValue(out result));
            }

            Assert.NotNull(result);
            Assert.Equal(0, result.Failed);
            Assert.True(result.IdempotencyRecords >= 1);

            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = new[] { expired.Id, current.Id };
                var left = await db.Set<IdempotencyRecord>().AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => r.Id).ToListAsync();

                Assert.DoesNotContain(expired.Id, left);
                Assert.Contains(current.Id, left);
            }
        }
    }



}
