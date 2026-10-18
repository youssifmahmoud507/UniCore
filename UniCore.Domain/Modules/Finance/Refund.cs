using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // Money going back to a student, with approval:
    //   Requested -> Approved -> Processed        or        Requested/Approved -> Rejected.
    // The invoice side (reserving the amount on the payment, then taking it off the paid amount) is done by Invoice.
    public sealed class Refund : AggregateRoot
    {
        public const int MaxReasonLength = 300;
        public const int MaxRejectionReasonLength = 200;

        private Refund()
        {
        }

        public Guid PaymentId { get; private set; }
        public Guid InvoiceId { get; private set; }
        public Guid StudentId { get; private set; }
        public Money Amount { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public RefundStatus Status { get; private set; }
        public Guid? RequestedByUserId { get; private set; }
        public DateTimeOffset RequestedAt { get; private set; }
        public Guid? ReviewedByUserId { get; private set; }
        public DateTimeOffset? ReviewedAt { get; private set; }
        public string? RejectionReason { get; private set; }
        public Guid? ProcessedByUserId { get; private set; }
        public DateTimeOffset? ProcessedAt { get; private set; }

        public static Result<Refund> Request(Guid paymentId, Guid invoiceId, Guid studentId, Money amount, string? reason, Guid? requestedByUserId, DateTimeOffset now)
        {
            var cleanReason = reason?.Trim();

            if (paymentId == Guid.Empty || invoiceId == Guid.Empty || studentId == Guid.Empty
                || string.IsNullOrEmpty(cleanReason) || cleanReason.Length > MaxReasonLength)
            {
                return FinanceErrors.RefundDataInvalid;
            }

            if (!amount.IsPositive)
            {
                return FinanceErrors.AmountMustBePositive;
            }

            return new Refund
            {
                Id = Guid.NewGuid(),
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                StudentId = studentId,
                Amount = amount,
                Reason = cleanReason,
                Status = RefundStatus.Requested,
                RequestedByUserId = requestedByUserId,
                RequestedAt = now
            };
        }

        public Result Approve(Guid? reviewerUserId, DateTimeOffset now)
        {
            if (Status != RefundStatus.Requested)
            {
                return Result.Fail(FinanceErrors.RefundInvalidState);
            }

            if (reviewerUserId is { } reviewer && RequestedByUserId == reviewer)
            {
                return Result.Fail(FinanceErrors.RefundSelfApproval);
            }

            Status = RefundStatus.Approved;
            ReviewedByUserId = reviewerUserId;
            ReviewedAt = now;

            return Result.Ok();
        }

        // A reason is required. A refund can be rejected until it is processed.
        public Result Reject(Guid? reviewerUserId, string? reason, DateTimeOffset now)
        {
            if (Status is not (RefundStatus.Requested or RefundStatus.Approved))
            {
                return Result.Fail(FinanceErrors.RefundInvalidState);
            }

            var cleanReason = reason?.Trim();

            if (string.IsNullOrEmpty(cleanReason) || cleanReason.Length > MaxRejectionReasonLength)
            {
                return Result.Fail(FinanceErrors.RefundDataInvalid);
            }

            Status = RefundStatus.Rejected;
            ReviewedByUserId = reviewerUserId;
            ReviewedAt = now;
            RejectionReason = cleanReason;

            return Result.Ok();
        }

        public Result MarkProcessed(Guid? processedByUserId, DateTimeOffset now)
        {
            if (Status != RefundStatus.Approved)
            {
                return Result.Fail(FinanceErrors.RefundInvalidState);
            }

            Status = RefundStatus.Processed;
            ProcessedByUserId = processedByUserId;
            ProcessedAt = now;

            return Result.Ok();
        }
    }



}
