using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Outbox;

namespace UniCore.UnitTests.Modules.Outbox
{
    public class OutboxMessageTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        private static OutboxMessage NewMessage(DateTimeOffset? notBefore = null)
        {
            var result = OutboxMessage.Create("Some.Event", "{\"a\":1}", Now.AddSeconds(-1), Now, notBefore);

            Assert.True(result.TryGetValue(out var message));
            Assert.NotNull(message);
            return message;
        }

        [Fact]
        public void A_new_message_is_pending_and_keeps_its_fields()
        {
            var message = NewMessage();

            Assert.NotEqual(Guid.Empty, message.Id);
            Assert.Equal("Some.Event", message.Type);
            Assert.Equal("{\"a\":1}", message.Payload);
            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Equal(0, message.RetryCount);
            Assert.Equal(Now, message.CreatedAt);
            Assert.Equal(Now.AddSeconds(-1), message.OccurredAt);
            Assert.Null(message.NextAttemptAt);
            Assert.Null(message.ProcessedAt);
        }

        [Fact]
        public void A_scheduled_message_waits_for_its_start_time()
        {
            var message = NewMessage(Now.AddHours(1));

            Assert.Equal(Now.AddHours(1), message.NextAttemptAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void A_blank_type_is_rejected(string? type)
        {
            var result = OutboxMessage.Create(type, "{}", Now, Now);

            Assert.True(result.IsFailure);
            Assert.Equal("OUTBOX_TYPE_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void A_type_longer_than_the_limit_is_rejected()
        {
            var result = OutboxMessage.Create(new string('T', OutboxMessage.MaxTypeLength + 1), "{}", Now, Now);

            Assert.True(result.IsFailure);
            Assert.Equal("OUTBOX_TYPE_INVALID", result.Errors[0].Code);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void A_blank_payload_is_rejected(string? payload)
        {
            var result = OutboxMessage.Create("Some.Event", payload, Now, Now);

            Assert.True(result.IsFailure);
            Assert.Equal("OUTBOX_PAYLOAD_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void Marking_processed_sets_the_time_and_can_wipe_the_payload()
        {
            var plain = NewMessage();
            var secret = NewMessage();

            Assert.True(plain.MarkProcessed(Now.AddSeconds(5), redactPayload: false).IsSuccess);
            Assert.True(secret.MarkProcessed(Now.AddSeconds(5), redactPayload: true).IsSuccess);

            Assert.Equal(OutboxMessageStatus.Processed, plain.Status);
            Assert.Equal(Now.AddSeconds(5), plain.ProcessedAt);
            Assert.Equal("{\"a\":1}", plain.Payload);
            Assert.Equal(OutboxMessage.RedactedPayload, secret.Payload);
        }

        [Fact]
        public void A_temporary_failure_goes_back_to_pending_with_a_later_attempt_time()
        {
            var message = NewMessage();

            var result = message.RecordFailure("EXTERNAL_SERVICE_UNAVAILABLE", permanent: false, Now, redactPayloadWhenFailed: false);

            Assert.True(result.IsSuccess);
            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Equal(1, message.RetryCount);
            Assert.Equal("EXTERNAL_SERVICE_UNAVAILABLE", message.LastErrorCode);
            Assert.Equal(Now.Add(OutboxRetryPolicy.DelayAfter(1)), message.NextAttemptAt);
            Assert.Null(message.LockedUntil);
        }

        [Fact]
        public void The_message_becomes_poison_after_the_maximum_number_of_attempts()
        {
            var message = NewMessage();

            for (var attempt = 1; attempt < OutboxRetryPolicy.MaxAttempts; attempt++)
            {
                Assert.True(message.RecordFailure("X", false, Now, false).IsSuccess);
                Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            }

            Assert.True(message.RecordFailure("X", false, Now, false).IsSuccess);

            Assert.Equal(OutboxMessageStatus.Failed, message.Status);
            Assert.Equal(OutboxRetryPolicy.MaxAttempts, message.RetryCount);
            Assert.Null(message.NextAttemptAt);
        }

        [Fact]
        public void A_permanent_failure_is_poison_immediately_and_can_wipe_the_payload()
        {
            var message = NewMessage();

            var result = message.RecordFailure("OUTBOX_PAYLOAD_INVALID", permanent: true, Now, redactPayloadWhenFailed: true);

            Assert.True(result.IsSuccess);
            Assert.Equal(OutboxMessageStatus.Failed, message.Status);
            Assert.Equal(1, message.RetryCount);
            Assert.Equal(OutboxMessage.RedactedPayload, message.Payload);
        }

        [Fact]
        public void A_long_error_code_is_shortened()
        {
            var message = NewMessage();

            message.RecordFailure(new string('E', OutboxMessage.MaxErrorCodeLength + 50), false, Now, false);

            Assert.Equal(OutboxMessage.MaxErrorCodeLength, message.LastErrorCode?.Length);
        }

        [Fact]
        public void A_finished_message_cannot_be_changed_again()
        {
            var processed = NewMessage();
            processed.MarkProcessed(Now, false);

            var failed = NewMessage();
            failed.RecordFailure("X", true, Now, false);

            Assert.Equal("OUTBOX_MESSAGE_FINISHED", processed.MarkProcessed(Now, false).Errors[0].Code);
            Assert.Equal("OUTBOX_MESSAGE_FINISHED", processed.RecordFailure("X", false, Now, false).Errors[0].Code);
            Assert.Equal("OUTBOX_MESSAGE_FINISHED", failed.MarkProcessed(Now, false).Errors[0].Code);
            Assert.Equal(1, failed.RetryCount);
        }

        [Fact]
        public void The_retry_delay_grows_and_is_capped_at_one_hour()
        {
            Assert.Equal(TimeSpan.FromSeconds(30), OutboxRetryPolicy.DelayAfter(1));
            Assert.Equal(TimeSpan.FromMinutes(2), OutboxRetryPolicy.DelayAfter(2));
            Assert.Equal(TimeSpan.FromMinutes(8), OutboxRetryPolicy.DelayAfter(3));
            Assert.Equal(TimeSpan.FromHours(1), OutboxRetryPolicy.DelayAfter(50));
            Assert.Equal(TimeSpan.FromSeconds(30), OutboxRetryPolicy.DelayAfter(0));
        }
    }
}
