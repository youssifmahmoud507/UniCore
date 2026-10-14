using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Students;
using UniCore.Infrastructure.Modules.Outbox;

namespace UniCore.IntegrationTests.Outbox
{
    public sealed class DomainEventOutboxHandlerTests
    {
        private sealed class RecordingHandler(Result? result = null) : IDomainEventHandler<StudentAdmitted>
        {
            public List<StudentAdmitted> Received { get; } = [];

            public Task<Result> HandleAsync(StudentAdmitted domainEvent, CancellationToken cancellationToken = default)
            {
                Received.Add(domainEvent);
                return Task.FromResult(result ?? Result.Ok());
            }
        }

        private static StudentAdmitted NewEvent() => new(Guid.NewGuid(), new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero), Guid.NewGuid(), Guid.NewGuid(), "2026CS0001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 1));

        private static ServiceProvider Provider(params IDomainEventHandler<StudentAdmitted>[] handlers)
        {
            var services = new ServiceCollection();

            foreach (var handler in handlers)
            {
                services.AddSingleton(handler);
            }

            return services.BuildServiceProvider();
        }

        [Fact]
        public void The_message_type_is_the_full_event_type_name()
        {
            Assert.Equal(typeof(StudentAdmitted).FullName, new DomainEventOutboxHandler<StudentAdmitted>().MessageType);
        }

        [Fact]
        public async Task A_stored_event_comes_back_unchanged_and_reaches_every_handler()
        {
            var original = NewEvent();
            var first = new RecordingHandler();
            var second = new RecordingHandler();
            var message = OutboxMessageSerializer.FromDomainEvent(original, DateTimeOffset.UtcNow);
            Assert.True(message.TryGetValue(out var stored));
            Assert.NotNull(stored);
            using var provider = Provider(first, second);

            var result = await new DomainEventOutboxHandler<StudentAdmitted>().HandleAsync(stored.Payload, provider, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(original, Assert.Single(first.Received));
            Assert.Equal(original, Assert.Single(second.Received));
        }

        [Fact]
        public async Task A_failing_handler_stops_the_chain_and_its_error_is_returned()
        {
            var failing = new RecordingHandler(Result.Fail(Error.External()));
            var after = new RecordingHandler();
            var payload = JsonSerializer.Serialize(NewEvent(), OutboxMessageSerializer.Options);
            using var provider = Provider(failing, after);

            var result = await new DomainEventOutboxHandler<StudentAdmitted>().HandleAsync(payload, provider, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal("EXTERNAL_SERVICE_UNAVAILABLE", result.Errors[0].Code);
            Assert.Empty(after.Received);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("null")]
        public async Task An_unreadable_payload_is_a_permanent_validation_error(string payload)
        {
            var handler = new RecordingHandler();
            using var provider = Provider(handler);

            var result = await new DomainEventOutboxHandler<StudentAdmitted>().HandleAsync(payload, provider, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal("OUTBOX_PAYLOAD_INVALID", result.Errors[0].Code);
            Assert.Equal(ErrorType.Validation, result.Errors[0].ErrorType);
            Assert.Empty(handler.Received);
        }

        [Fact]
        public async Task An_event_with_no_consumers_is_still_a_success()
        {
            using var provider = Provider();
            var payload = JsonSerializer.Serialize(NewEvent(), OutboxMessageSerializer.Options);

            var result = await new DomainEventOutboxHandler<StudentAdmitted>().HandleAsync(payload, provider, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }
    }
}
