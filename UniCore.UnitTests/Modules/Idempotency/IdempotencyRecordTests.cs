using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Idempotency;

namespace UniCore.UnitTests.Modules.Idempotency
{
    public class IdempotencyRecordTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Day = TimeSpan.FromHours(24);
        private static readonly string Hash = new('a', IdempotencyRecord.HashLength);

        private static IdempotencyRecord NewRecord()
        {
            var begun = IdempotencyRecord.Begin("key-12345678", Guid.NewGuid(), Hash, Now, Day);
            Assert.True(begun.TryGetValue(out var record));
            Assert.NotNull(record);
            return record;
        }

        [Fact]
        public void Begin_creates_a_running_record_that_expires_after_its_lifetime()
        {
            var userId = Guid.NewGuid();

            var begun = IdempotencyRecord.Begin("  key-12345678  ", userId, Hash, Now, Day);

            Assert.True(begun.TryGetValue(out var record));
            Assert.NotNull(record);
            Assert.NotEqual(Guid.Empty, record.Id);
            Assert.Equal("key-12345678", record.Key);
            Assert.Equal(userId, record.UserId);
            Assert.Equal(Hash, record.RequestHash);
            Assert.Equal(IdempotencyRecordStatus.InProgress, record.Status);
            Assert.Equal(Now, record.CreatedAt);
            Assert.Equal(Now.Add(Day), record.ExpiresAt);
            Assert.Null(record.ResponseBody);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("short")]
        [InlineData("has a space in it")]
        [InlineData("tab\tinside-key")]
        [InlineData("caf\u00e9-key-12345")]
        public void Begin_rejects_a_bad_key(string? key)
        {
            var begun = IdempotencyRecord.Begin(key, Guid.NewGuid(), Hash, Now, Day);

            Assert.True(begun.IsFailure);
            Assert.Equal("IDEMPOTENCY_KEY_INVALID", begun.Errors[0].Code);
        }

        [Fact]
        public void Begin_rejects_a_key_longer_than_100_characters()
        {
            var begun = IdempotencyRecord.Begin(new string('k', IdempotencyRecord.MaxKeyLength + 1), Guid.NewGuid(), Hash, Now, Day);

            Assert.Equal("IDEMPOTENCY_KEY_INVALID", begun.Errors[0].Code);
            Assert.True(IdempotencyRecord.Begin(new string('k', IdempotencyRecord.MaxKeyLength), Guid.NewGuid(), Hash, Now, Day).IsSuccess);
        }

        [Fact]
        public void Begin_rejects_a_missing_user_a_bad_hash_and_a_zero_lifetime()
        {
            var noUser = IdempotencyRecord.Begin("key-12345678", Guid.Empty, Hash, Now, Day);
            var shortHash = IdempotencyRecord.Begin("key-12345678", Guid.NewGuid(), "abc", Now, Day);
            var notHex = IdempotencyRecord.Begin("key-12345678", Guid.NewGuid(), new string('z', IdempotencyRecord.HashLength), Now, Day);
            var noHash = IdempotencyRecord.Begin("key-12345678", Guid.NewGuid(), null, Now, Day);
            var noLifetime = IdempotencyRecord.Begin("key-12345678", Guid.NewGuid(), Hash, Now, TimeSpan.Zero);

            foreach (var result in new[] { noUser, shortHash, notHex, noHash, noLifetime })
            {
                Assert.True(result.IsFailure);
                Assert.Equal("IDEMPOTENCY_RECORD_INVALID", result.Errors[0].Code);
            }
        }

        [Fact]
        public void Complete_keeps_the_response_and_a_record_completes_only_once()
        {
            var record = NewRecord();
            var body = new byte[] { 1, 2, 3 };

            var completed = record.Complete(201, "application/json", "/api/v1/things/1", body, Now.AddSeconds(2));

            Assert.True(completed.IsSuccess);
            Assert.Equal(IdempotencyRecordStatus.Completed, record.Status);
            Assert.Equal(Now.AddSeconds(2), record.CompletedAt);
            Assert.Equal(201, record.ResponseStatus);
            Assert.Equal("application/json", record.ResponseContentType);
            Assert.Equal("/api/v1/things/1", record.ResponseLocation);
            Assert.Equal(body, record.ResponseBody);

            var again = record.Complete(200, null, null, body, Now.AddSeconds(3));

            Assert.True(again.IsFailure);
            Assert.Equal("IDEMPOTENCY_RECORD_COMPLETED", again.Errors[0].Code);
            Assert.Equal(201, record.ResponseStatus);
        }

        [Theory]
        [InlineData(199)]
        [InlineData(300)]
        [InlineData(422)]
        [InlineData(500)]
        public void Only_a_success_response_can_be_kept(int status)
        {
            var record = NewRecord();

            var completed = record.Complete(status, null, null, [1], Now);

            Assert.True(completed.IsFailure);
            Assert.Equal("IDEMPOTENCY_RECORD_INVALID", completed.Errors[0].Code);
            Assert.Equal(IdempotencyRecordStatus.InProgress, record.Status);
        }

        [Fact]
        public void Complete_needs_a_body_and_reasonable_header_values()
        {
            var record = NewRecord();

            Assert.True(record.Complete(200, null, null, null, Now).IsFailure);
            Assert.True(record.Complete(200, new string('c', IdempotencyRecord.MaxContentTypeLength + 1), null, [1], Now).IsFailure);
            Assert.True(record.Complete(200, null, new string('l', IdempotencyRecord.MaxLocationLength + 1), [1], Now).IsFailure);
            Assert.True(record.Complete(204, null, null, [], Now).IsSuccess);
        }

        [Fact]
        public void A_record_is_expired_from_its_expiry_moment()
        {
            var record = NewRecord();

            Assert.False(record.IsExpired(Now.Add(Day).AddTicks(-1)));
            Assert.True(record.IsExpired(Now.Add(Day)));
            Assert.True(record.IsExpired(Now.Add(Day).AddHours(1)));
        }

        [Fact]
        public void Only_a_running_record_can_be_stale()
        {
            var running = NewRecord();
            var timeout = TimeSpan.FromMinutes(2);

            Assert.False(running.IsStale(Now.AddMinutes(1), timeout));
            Assert.True(running.IsStale(Now.AddMinutes(2), timeout));

            Assert.True(running.Complete(200, null, null, [1], Now.AddSeconds(1)).IsSuccess);
            Assert.False(running.IsStale(Now.AddHours(5), timeout));
        }

        [Fact]
        public void A_request_matches_only_its_own_hash()
        {
            var record = NewRecord();

            Assert.True(record.MatchesRequest(Hash));
            Assert.False(record.MatchesRequest(new string('b', IdempotencyRecord.HashLength)));
            Assert.False(record.MatchesRequest(null));
            Assert.False(record.MatchesRequest(Hash.ToUpperInvariant()));
        }

        [Theory]
        [InlineData("key-12345678", "key-12345678")]
        [InlineData("  key-12345678\t", "key-12345678")]
        [InlineData("1234567", null)]
        [InlineData(null, null)]
        public void NormalizeKey_trims_and_validates(string? input, string? expected)
            => Assert.Equal(expected, IdempotencyRecord.NormalizeKey(input));
    }
}
