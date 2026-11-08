using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Idempotency
{
    // "I already handled this Idempotency-Key for this user." Unique on (UserId, Key), which is what makes two
    // parallel identical requests safe: only one of them can insert the row.
    public sealed class IdempotencyRecord : BaseEntity
    {
        public const int MinKeyLength = 8;
        public const int MaxKeyLength = 100;
        public const int HashLength = 64;
        public const int MaxContentTypeLength = 200;
        public const int MaxLocationLength = 2000;

        private IdempotencyRecord()
        {
        }

        public string Key { get; private set; } = string.Empty;
        public Guid UserId { get; private set; }

        // SHA-256 (hex) of the method, the path and the body. The same key with another hash is a different request.
        public string RequestHash { get; private set; } = string.Empty;
        public IdempotencyRecordStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public int? ResponseStatus { get; private set; }
        public string? ResponseContentType { get; private set; }
        public string? ResponseLocation { get; private set; }
        public byte[]? ResponseBody { get; private set; }

        // 8 to 100 visible ASCII characters, no spaces. Null means invalid.
        public static string? NormalizeKey(string? key)
        {
            var clean = key?.Trim();

            if (string.IsNullOrEmpty(clean) || clean.Length < MinKeyLength || clean.Length > MaxKeyLength)
            {
                return null;
            }

            return clean.All(c => c is > ' ' and < '\u007f') ? clean : null;
        }

        public static Result<IdempotencyRecord> Begin(string? key, Guid userId, string? requestHash, DateTimeOffset now, TimeSpan lifetime)
        {
            var cleanKey = NormalizeKey(key);

            if (cleanKey is null)
            {
                return IdempotencyErrors.KeyInvalid;
            }

            if (userId == Guid.Empty || !IsValidHash(requestHash) || lifetime <= TimeSpan.Zero)
            {
                return IdempotencyErrors.RecordDataInvalid;
            }

            return new IdempotencyRecord
            {
                Id = Guid.NewGuid(),
                Key = cleanKey,
                UserId = userId,
                RequestHash = requestHash!,
                Status = IdempotencyRecordStatus.InProgress,
                CreatedAt = now,
                ExpiresAt = now.Add(lifetime)
            };
        }

        public Result Complete(int statusCode, string? contentType, string? location, byte[]? body, DateTimeOffset now)
        {
            if (Status == IdempotencyRecordStatus.Completed)
            {
                return Result.Fail(IdempotencyErrors.RecordAlreadyCompleted);
            }

            if (statusCode is < 200 or > 299 || body is null || (contentType?.Length ?? 0) > MaxContentTypeLength || (location?.Length ?? 0) > MaxLocationLength)
            {
                return Result.Fail(IdempotencyErrors.RecordDataInvalid);
            }

            Status = IdempotencyRecordStatus.Completed;
            CompletedAt = now;
            ResponseStatus = statusCode;
            ResponseContentType = contentType;
            ResponseLocation = location;
            ResponseBody = body;

            return Result.Ok();
        }

        public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

        // A request that has been "running" for longer than this crashed. Its key may be used again.
        public bool IsStale(DateTimeOffset now, TimeSpan inProgressTimeout)
            => Status == IdempotencyRecordStatus.InProgress && CreatedAt.Add(inProgressTimeout) <= now;

        public bool MatchesRequest(string? requestHash) => string.Equals(RequestHash, requestHash, StringComparison.Ordinal);

        private static bool IsValidHash(string? hash)
            => hash is { Length: HashLength } && hash.All(Uri.IsHexDigit);
    }



}
