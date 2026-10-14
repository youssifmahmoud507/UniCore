using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Outbox;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.Infrastructure.Persistence;
using UniCore.IntegrationTests.Infrastructure;

namespace UniCore.IntegrationTests.Outbox
{
    // The test messages are scheduled one day ahead of real time, so the API's background processor ignores them;
    // each test's processor uses a clock two days ahead, so for it the messages are due.
    [Collection(ApiCollection.Name)]
    public sealed class OutboxProcessorTests(ApiFixture fixture)
    {
        private sealed class ScriptedHandler(string type, Func<string, Task<Result>> handle, bool redact = false) : IOutboxMessageHandler
        {
            public int Calls;

            public string MessageType { get; } = type;

            public bool RedactPayloadWhenDone { get; } = redact;

            public Task<Result> HandleAsync(string payload, IServiceProvider services, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                return handle(payload);
            }
        }

        private static string NewType() => $"test.{Guid.NewGuid():N}";

        private MutableClock NewClock() => new(DateTimeOffset.UtcNow.AddDays(2));

        private OutboxProcessor NewProcessor(MutableClock clock, params IOutboxMessageHandler[] handlers)
            => new(fixture.Services.GetRequiredService<IServiceScopeFactory>(), handlers, clock, NullLogger<OutboxProcessor>.Instance);

        private async Task<Guid> InsertAsync(string type, string payload = "{\"secret\":1}")
        {
            var created = OutboxMessage.Create(type, payload, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));
            Assert.True(created.TryGetValue(out var message));
            Assert.NotNull(message);

            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<OutboxMessage>().Add(message);
            await db.SaveChangesAsync();

            return message.Id;
        }

        private async Task<OutboxMessage> LoadAsync(Guid id)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = await db.Set<OutboxMessage>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);

            Assert.NotNull(message);
            return message;
        }

        [Fact]
        public async Task A_successful_handler_marks_the_message_processed()
        {
            var type = NewType();
            var handler = new ScriptedHandler(type, _ => Task.FromResult(Result.Ok()));
            var id = await InsertAsync(type);

            var outcome = await NewProcessor(NewClock(), handler).ProcessOneAsync(id, CancellationToken.None);

            Assert.True(outcome.IsSuccess);
            Assert.True(outcome.Value);
            Assert.Equal(1, handler.Calls);

            var stored = await LoadAsync(id);
            Assert.Equal(OutboxMessageStatus.Processed, stored.Status);
            Assert.NotNull(stored.ProcessedAt);
            Assert.Equal("{\"secret\":1}", stored.Payload);
        }

        [Fact]
        public async Task A_sensitive_message_has_its_payload_wiped_when_done()
        {
            var type = NewType();
            var handler = new ScriptedHandler(type, _ => Task.FromResult(Result.Ok()), redact: true);
            var id = await InsertAsync(type);

            await NewProcessor(NewClock(), handler).ProcessOneAsync(id, CancellationToken.None);

            var stored = await LoadAsync(id);
            Assert.Equal(OutboxMessageStatus.Processed, stored.Status);
            Assert.Equal(OutboxMessage.RedactedPayload, stored.Payload);
        }

        [Fact]
        public async Task A_temporary_failure_is_retried_later_and_becomes_poison_after_the_maximum_attempts()
        {
            var type = NewType();
            var handler = new ScriptedHandler(type, _ => Task.FromResult(Result.Fail(Error.External())));
            var id = await InsertAsync(type);
            var clock = NewClock();
            var processor = NewProcessor(clock, handler);

            for (var attempt = 1; attempt <= OutboxRetryPolicy.MaxAttempts; attempt++)
            {
                var outcome = await processor.ProcessOneAsync(id, CancellationToken.None);
                Assert.True(outcome.Value);

                var stored = await LoadAsync(id);
                Assert.Equal(attempt, stored.RetryCount);
                Assert.Equal("EXTERNAL_SERVICE_UNAVAILABLE", stored.LastErrorCode);

                if (attempt < OutboxRetryPolicy.MaxAttempts)
                {
                    Assert.Equal(OutboxMessageStatus.Pending, stored.Status);

                    // Not due yet: nothing happens until the delay has passed.
                    var tooEarly = await processor.ProcessOneAsync(id, CancellationToken.None);
                    Assert.False(tooEarly.Value);

                    clock.Advance(OutboxRetryPolicy.DelayAfter(attempt) + TimeSpan.FromSeconds(1));
                }
            }

            var poison = await LoadAsync(id);
            Assert.Equal(OutboxMessageStatus.Failed, poison.Status);
            Assert.Equal(OutboxRetryPolicy.MaxAttempts, handler.Calls);

            // A poison message is never picked up again.
            clock.Advance(TimeSpan.FromDays(1));
            var again = await processor.ProcessOneAsync(id, CancellationToken.None);
            Assert.False(again.Value);
            Assert.Equal(OutboxRetryPolicy.MaxAttempts, handler.Calls);
        }

        [Fact]
        public async Task A_permanent_failure_is_poison_after_one_attempt()
        {
            var type = NewType();
            var handler = new ScriptedHandler(type, _ => Task.FromResult(Result.Fail(Error.Validation("BAD_INPUT", "bad"))));
            var id = await InsertAsync(type);

            await NewProcessor(NewClock(), handler).ProcessOneAsync(id, CancellationToken.None);

            var stored = await LoadAsync(id);
            Assert.Equal(OutboxMessageStatus.Failed, stored.Status);
            Assert.Equal(1, stored.RetryCount);
            Assert.Equal("BAD_INPUT", stored.LastErrorCode);
        }

        [Fact]
        public async Task A_message_with_no_registered_handler_is_poison()
        {
            var id = await InsertAsync(NewType());

            await NewProcessor(NewClock()).ProcessOneAsync(id, CancellationToken.None);

            var stored = await LoadAsync(id);
            Assert.Equal(OutboxMessageStatus.Failed, stored.Status);
            Assert.Equal("OUTBOX_HANDLER_NOT_FOUND", stored.LastErrorCode);
        }

        [Fact]
        public async Task A_handler_that_crashes_is_retried_and_does_not_stop_the_processor()
        {
            var type = NewType();

            // Convert.ToInt32 fails with a FormatException: the crash comes from the framework, not from our code.
            var handler = new ScriptedHandler(type, _ => Task.FromResult(Convert.ToInt32("not a number") > 0 ? Result.Ok() : Result.Ok()));
            var id = await InsertAsync(type);

            var outcome = await NewProcessor(NewClock(), handler).ProcessOneAsync(id, CancellationToken.None);

            Assert.True(outcome.IsSuccess);

            var stored = await LoadAsync(id);
            Assert.Equal(OutboxMessageStatus.Pending, stored.Status);
            Assert.Equal(1, stored.RetryCount);
            Assert.Equal("OUTBOX_HANDLER_FAILED", stored.LastErrorCode);
        }

        [Fact]
        public async Task Two_processors_racing_for_one_message_run_its_handler_once()
        {
            var type = NewType();
            var handler = new ScriptedHandler(type, async _ =>
            {
                await Task.Delay(300);
                return Result.Ok();
            });
            var id = await InsertAsync(type);
            var clock = NewClock();

            var first = NewProcessor(clock, handler).ProcessOneAsync(id, CancellationToken.None);
            var second = NewProcessor(clock, handler).ProcessOneAsync(id, CancellationToken.None);
            var outcomes = await Task.WhenAll(first, second);

            Assert.Equal(1, handler.Calls);
            Assert.Equal(1, outcomes.Count(o => o.IsSuccess && o.Value));

            var stored = await LoadAsync(id);
            Assert.Equal(OutboxMessageStatus.Processed, stored.Status);
        }

        [Fact]
        public async Task An_expired_claim_is_taken_over()
        {
            var type = NewType();
            var handler = new ScriptedHandler(type, _ => Task.FromResult(Result.Ok()));
            var id = await InsertAsync(type);
            var clock = NewClock();

            // Simulate a processor that claimed the message and then died.
            await using (var scope = fixture.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DateTimeOffset? lockedUntil = clock.UtcNow.AddMinutes(5);

                await db.Set<OutboxMessage>().Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, OutboxMessageStatus.Processing).SetProperty(m => m.LockedUntil, lockedUntil));
            }

            var processor = NewProcessor(clock, handler);

            var whileLocked = await processor.ProcessOneAsync(id, CancellationToken.None);
            Assert.False(whileLocked.Value);

            clock.Advance(OutboxProcessor.LockDuration + TimeSpan.FromMinutes(1));

            var afterExpiry = await processor.ProcessOneAsync(id, CancellationToken.None);
            Assert.True(afterExpiry.Value);
            Assert.Equal(1, handler.Calls);
        }
    }

}
