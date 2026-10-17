using UniCore.Domain.Modules.Finance;

namespace UniCore.UnitTests.Modules.Finance
{
    public class RefundTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
        private static readonly Guid Requester = Guid.NewGuid();
        private static readonly Guid Approver = Guid.NewGuid();

        private static Money Amount(decimal value)
        {
            Assert.True(Money.Of(value).TryGetValue(out var money));
            return money;
        }

        private static Refund NewRefund()
        {
            var result = Refund.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Amount(100m), "  Paid twice  ", Requester, Now);
            Assert.True(result.TryGetValue(out var refund));
            Assert.NotNull(refund);
            return refund;
        }

        [Fact]
        public void A_new_refund_is_requested()
        {
            var refund = NewRefund();

            Assert.Equal(RefundStatus.Requested, refund.Status);
            Assert.Equal("Paid twice", refund.Reason);
            Assert.Equal(100m, refund.Amount.Amount);
            Assert.Equal(Requester, refund.RequestedByUserId);
            Assert.Equal(Now, refund.RequestedAt);
        }

        [Fact]
        public void Bad_data_is_rejected()
        {
            Assert.Equal("REFUND_DATA_INVALID", Refund.Request(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Amount(1m), "x", null, Now).Errors[0].Code);
            Assert.Equal("REFUND_DATA_INVALID", Refund.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Amount(1m), "   ", null, Now).Errors[0].Code);
            Assert.Equal("REFUND_DATA_INVALID", Refund.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Amount(1m), new string('r', Refund.MaxReasonLength + 1), null, Now).Errors[0].Code);
            Assert.Equal("FINANCE_AMOUNT_MUST_BE_POSITIVE", Refund.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.Zero, "x", null, Now).Errors[0].Code);
        }

        [Fact]
        public void Another_person_can_approve_and_then_the_refund_can_be_processed()
        {
            var refund = NewRefund();

            Assert.True(refund.Approve(Approver, Now.AddHours(1)).IsSuccess);
            Assert.Equal(RefundStatus.Approved, refund.Status);
            Assert.Equal(Approver, refund.ReviewedByUserId);

            Assert.True(refund.MarkProcessed(Approver, Now.AddHours(2)).IsSuccess);
            Assert.Equal(RefundStatus.Processed, refund.Status);
            Assert.Equal(Now.AddHours(2), refund.ProcessedAt);
        }

        [Fact]
        public void The_person_who_asked_cannot_approve()
        {
            var refund = NewRefund();

            var result = refund.Approve(Requester, Now);

            Assert.Equal("REFUND_SELF_APPROVAL", result.Errors[0].Code);
            Assert.Equal(RefundStatus.Requested, refund.Status);
        }

        [Fact]
        public void A_refund_cannot_be_approved_twice_or_processed_before_approval()
        {
            var refund = NewRefund();

            Assert.Equal("REFUND_INVALID_STATE", refund.MarkProcessed(Approver, Now).Errors[0].Code);

            refund.Approve(Approver, Now);

            Assert.Equal("REFUND_INVALID_STATE", refund.Approve(Approver, Now).Errors[0].Code);

            refund.MarkProcessed(Approver, Now);

            Assert.Equal("REFUND_INVALID_STATE", refund.MarkProcessed(Approver, Now).Errors[0].Code);
        }

        [Fact]
        public void A_refund_can_be_rejected_before_it_is_processed_and_the_reason_is_required()
        {
            var requested = NewRefund();
            var approved = NewRefund();
            approved.Approve(Approver, Now);

            Assert.Equal("REFUND_DATA_INVALID", requested.Reject(Approver, "  ", Now).Errors[0].Code);
            Assert.Equal(RefundStatus.Requested, requested.Status);

            Assert.True(requested.Reject(Approver, " Not allowed ", Now).IsSuccess);
            Assert.True(approved.Reject(Approver, "Changed my mind", Now).IsSuccess);
            Assert.Equal("Not allowed", requested.RejectionReason);
            Assert.Equal(RefundStatus.Rejected, approved.Status);
        }

        [Fact]
        public void A_processed_or_rejected_refund_cannot_be_rejected()
        {
            var processed = NewRefund();
            processed.Approve(Approver, Now);
            processed.MarkProcessed(Approver, Now);

            var rejected = NewRefund();
            rejected.Reject(Approver, "No", Now);

            Assert.Equal("REFUND_INVALID_STATE", processed.Reject(Approver, "x", Now).Errors[0].Code);
            Assert.Equal("REFUND_INVALID_STATE", rejected.Reject(Approver, "x", Now).Errors[0].Code);
            Assert.Equal("REFUND_INVALID_STATE", rejected.Approve(Approver, Now).Errors[0].Code);
        }
    }

}
