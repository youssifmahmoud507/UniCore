using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Domain.Modules.Audit;

namespace UniCore.UnitTests.Modules.Audit
{
    public class AuditLogTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void A_valid_entry_keeps_every_field()
        {
            var actor = Guid.NewGuid();

            var result = AuditLog.Create(AuditActions.UserCreated, actor, "User", "42", "10.0.0.1", "agent", "old", "new", "corr-1", Now);

            Assert.True(result.TryGetValue(out var log));
            Assert.NotNull(log);
            Assert.NotEqual(Guid.Empty, log.Id);
            Assert.Equal(AuditActions.UserCreated, log.Action);
            Assert.Equal(actor, log.ActorUserId);
            Assert.Equal("User", log.EntityName);
            Assert.Equal("42", log.EntityId);
            Assert.Equal("10.0.0.1", log.IpAddress);
            Assert.Equal("agent", log.UserAgent);
            Assert.Equal("old", log.OldValues);
            Assert.Equal("new", log.NewValues);
            Assert.Equal("corr-1", log.CorrelationId);
            Assert.Equal(Now, log.CreatedAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void A_blank_action_is_rejected(string? action)
        {
            var result = AuditLog.Create(action, null, null, null, null, null, null, null, null, Now);

            Assert.True(result.IsFailure);
            Assert.Equal("AUDIT_ACTION_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void An_action_longer_than_the_limit_is_rejected()
        {
            var result = AuditLog.Create(new string('A', AuditLog.MaxActionLength + 1), null, null, null, null, null, null, null, null, Now);

            Assert.True(result.IsFailure);
            Assert.Equal("AUDIT_ACTION_INVALID", result.Errors[0].Code);
        }

        [Fact]
        public void Control_characters_are_removed_and_long_values_are_capped()
        {
            var result = AuditLog.Create(
                "ACTION", null, null, null, null,
                "agent\r\nINJECTED\tline",
                null,
                new string('x', AuditLog.MaxValuesLength + 500),
                null, Now);

            Assert.True(result.TryGetValue(out var log));
            Assert.NotNull(log);
            Assert.Equal("agentINJECTEDline", log.UserAgent);
            Assert.NotNull(log.NewValues);
            Assert.Equal(AuditLog.MaxValuesLength, log.NewValues.Length);
        }

        [Fact]
        public void Blank_optional_fields_become_null()
        {
            var result = AuditLog.Create("ACTION", null, " ", "", "  ", null, " ", "", " ", Now);

            Assert.True(result.TryGetValue(out var log));
            Assert.NotNull(log);
            Assert.Null(log.EntityName);
            Assert.Null(log.EntityId);
            Assert.Null(log.IpAddress);
            Assert.Null(log.OldValues);
            Assert.Null(log.NewValues);
            Assert.Null(log.CorrelationId);
        }
    }



}
