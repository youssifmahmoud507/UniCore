using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Outbox
{
    // One thing that must happen after a transaction commits (a domain event, an email).
    // Delivery is at-least-once, so every handler must be idempotent.
    public sealed class OutboxMessage : BaseEntity
    {
        public const int MaxTypeLength = 300;
        public const int MaxErrorCodeLength = 100;

        // What the payload becomes once a message with sensitive content (an OTP email) is done.
        public const string RedactedPayload = "{}";

        private OutboxMessage()
        {
        }

        public string Type { get; private set; } = string.Empty;
        public string Payload { get; private set; } = string.Empty;
        public DateTimeOffset OccurredAt { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public OutboxMessageStatus Status { get; private set; }
        public int RetryCount { get; private set; }
        public DateTimeOffset? NextAttemptAt { get; private set; }
        public DateTimeOffset? LockedUntil { get; private set; }
        public DateTimeOffset? ProcessedAt { get; private set; }

        // The error CODE of the last failure. Never a message (it can contain personal data).
        public string? LastErrorCode { get; private set; }

        // notBefore delays the first attempt. Null means "as soon as possible".
        public static Result<OutboxMessage> Create(string? type, string? payload, DateTimeOffset occurredAt, DateTimeOffset now, DateTimeOffset? notBefore = null)
        {
            var cleanType = type?.Trim();

            if (string.IsNullOrEmpty(cleanType) || cleanType.Length > MaxTypeLength)
            {
                return OutboxErrors.TypeInvalid;
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                return OutboxErrors.PayloadInvalid;
            }

            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = cleanType,
                Payload = payload,
                OccurredAt = occurredAt,
                CreatedAt = now,
                Status = OutboxMessageStatus.Pending,
                NextAttemptAt = notBefore
            };
        }

        public Result MarkProcessed(DateTimeOffset now, bool redactPayload)
        {
            if (IsFinished)
            {
                return Result.Fail(OutboxErrors.MessageFinished);
            }

            Status = OutboxMessageStatus.Processed;
            ProcessedAt = now;
            NextAttemptAt = null;
            LockedUntil = null;
            LastErrorCode = null;

            if (redactPayload)
            {
                Payload = RedactedPayload;
            }

            return Result.Ok();
        }

        // Permanent failure goes straight to Failed. Any other failure waits for the next attempt
        // and becomes Failed after OutboxRetryPolicy.MaxAttempts.
        public Result RecordFailure(string? errorCode, bool permanent, DateTimeOffset now, bool redactPayloadWhenFailed)
        {
            if (IsFinished)
            {
                return Result.Fail(OutboxErrors.MessageFinished);
            }

            RetryCount++;
            LastErrorCode = CleanCode(errorCode);
            LockedUntil = null;

            if (permanent || RetryCount >= OutboxRetryPolicy.MaxAttempts)
            {
                Status = OutboxMessageStatus.Failed;
                NextAttemptAt = null;

                if (redactPayloadWhenFailed)
                {
                    Payload = RedactedPayload;
                }

                return Result.Ok();
            }

            Status = OutboxMessageStatus.Pending;
            NextAttemptAt = now.Add(OutboxRetryPolicy.DelayAfter(RetryCount));

            return Result.Ok();
        }

        private bool IsFinished => Status is OutboxMessageStatus.Processed or OutboxMessageStatus.Failed;

        private static string? CleanCode(string? code)
        {
            var clean = code?.Trim();

            if (string.IsNullOrEmpty(clean))
            {
                return null;
            }

            return clean.Length <= MaxErrorCodeLength ? clean : clean[..MaxErrorCodeLength];
        }
    }



}
