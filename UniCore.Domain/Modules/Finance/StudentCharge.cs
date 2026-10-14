using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // One fee that one student owes for one semester. Generated from an active FeeStructure.
    // Description and Amount are snapshots: changing the fee catalog later never changes a charge.
    public sealed class StudentCharge : AggregateRoot
    {
        public const int MaxDescriptionLength = 150;

        private StudentCharge()
        {
        }

        public Guid StudentId { get; private set; }
        public Guid AcademicSemesterId { get; private set; }
        public Guid FeeStructureItemId { get; private set; }
        public Guid FeeDefinitionId { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public Money Amount { get; private set; }
        public DateOnly DueDate { get; private set; }
        public ChargeStatus Status { get; private set; }
        public Guid? InvoiceId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public static Result<StudentCharge> Create(Guid studentId, Guid academicSemesterId, Guid feeStructureItemId, Guid feeDefinitionId, string? description, Money amount, DateOnly dueDate, DateTimeOffset now)
        {
            var text = description?.Trim();

            if (studentId == Guid.Empty
                || academicSemesterId == Guid.Empty
                || feeStructureItemId == Guid.Empty
                || feeDefinitionId == Guid.Empty
                || string.IsNullOrEmpty(text)
                || text.Length > MaxDescriptionLength)
            {
                return FinanceErrors.ChargeDataInvalid;
            }

            if (!amount.IsPositive)
            {
                return FinanceErrors.AmountMustBePositive;
            }

            return new StudentCharge
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                AcademicSemesterId = academicSemesterId,
                FeeStructureItemId = feeStructureItemId,
                FeeDefinitionId = feeDefinitionId,
                Description = text,
                Amount = amount,
                DueDate = dueDate,
                Status = ChargeStatus.Pending,
                CreatedAt = now
            };
        }

        public Result MarkInvoiced(Guid invoiceId, DateTimeOffset now)
        {
            if (invoiceId == Guid.Empty)
            {
                return Result.Fail(FinanceErrors.ChargeDataInvalid);
            }

            if (Status != ChargeStatus.Pending)
            {
                return Result.Fail(FinanceErrors.ChargeNotPending);
            }

            Status = ChargeStatus.Invoiced;
            InvoiceId = invoiceId;
            UpdatedAt = now;

            return Result.Ok();
        }

        public Result Cancel(DateTimeOffset now)
        {
            if (Status != ChargeStatus.Pending)
            {
                return Result.Fail(FinanceErrors.ChargeNotPending);
            }

            Status = ChargeStatus.Cancelled;
            UpdatedAt = now;

            return Result.Ok();
        }

        // When the invoice that holds this charge is cancelled, the charge is cancelled with it.
        // A cancelled charge can be generated again.
        public Result CancelWithInvoice(Guid invoiceId, DateTimeOffset now)
        {
            if (Status != ChargeStatus.Invoiced || InvoiceId != invoiceId)
            {
                return Result.Fail(FinanceErrors.ChargeNotInvoiced);
            }

            Status = ChargeStatus.Cancelled;
            UpdatedAt = now;

            return Result.Ok();
        }
    }


}
