using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // One recorded payment. Append-only: a payment is never changed or deleted (money going back is a Refund, in F4).
    // Only Invoice creates payments.
    public sealed class Payment : BaseEntity
    {
        public const int MaxReferenceLength = 100;
        public const int MinKeyLength = 8;
        public const int MaxKeyLength = 100;

        private Payment()
        {
        }

        public Guid InvoiceId { get; private set; }
        public Money Amount { get; private set; }
        public DateOnly PaymentDate { get; private set; }
        public PaymentMethod Method { get; private set; }
        public string? Reference { get; private set; }
        public string IdempotencyKey { get; private set; } = string.Empty;
        public Guid? RecordedByUserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        // Refunds requested or approved or processed (not rejected). Never more than Amount.
        public Money RefundCommittedAmount { get; private set; }

        // Refunds already processed.
        public Money RefundedAmount { get; private set; }

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

        internal static Result<Payment> Create(Guid invoiceId, Money amount, DateOnly paymentDate, PaymentMethod method, string? reference, string? idempotencyKey, Guid? recordedByUserId, DateTimeOffset now)
        {
            var key = NormalizeKey(idempotencyKey);

            if (key is null)
            {
                return FinanceErrors.IdempotencyKeyInvalid;
            }

            if (invoiceId == Guid.Empty || !Enum.IsDefined(method))
            {
                return FinanceErrors.PaymentDataInvalid;
            }

            var cleanReference = reference?.Trim();

            if (string.IsNullOrEmpty(cleanReference))
            {
                cleanReference = null;
            }
            else if (cleanReference.Length > MaxReferenceLength)
            {
                return FinanceErrors.PaymentDataInvalid;
            }

            // A bank transfer or a cheque must be traceable.
            if (cleanReference is null && method is PaymentMethod.BankTransfer or PaymentMethod.Cheque)
            {
                return FinanceErrors.PaymentReferenceRequired;
            }

            return new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Amount = amount,
                PaymentDate = paymentDate,
                Method = method,
                Reference = cleanReference,
                IdempotencyKey = key,
                RecordedByUserId = recordedByUserId,
                CreatedAt = now
            };
        }

        internal Result CommitRefund(Money amount)
        {
            var sum = RefundCommittedAmount.Add(amount);

            if (!sum.TryGetValue(out var committed))
            {
                return Result.Fail(sum.Errors);
            }

            if (committed > Amount)
            {
                return Result.Fail(FinanceErrors.RefundExceedsPayment);
            }

            RefundCommittedAmount = committed;

            return Result.Ok();
        }

        internal Result ReleaseRefund(Money amount)
        {
            var left = RefundCommittedAmount.Subtract(amount);

            if (!left.TryGetValue(out var committed))
            {
                return Result.Fail(left.Errors);
            }

            if (committed.IsNegative || committed < RefundedAmount)
            {
                return Result.Fail(FinanceErrors.RefundInvalidState);
            }

            RefundCommittedAmount = committed;

            return Result.Ok();
        }

        internal Result CompleteRefund(Money amount)
        {
            var sum = RefundedAmount.Add(amount);

            if (!sum.TryGetValue(out var refunded))
            {
                return Result.Fail(sum.Errors);
            }

            if (refunded > RefundCommittedAmount)
            {
                return Result.Fail(FinanceErrors.RefundInvalidState);
            }

            RefundedAmount = refunded;

            return Result.Ok();
        }

    }




}
