using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Finance
{
    // One fee that one student owes. Two kinds:
    //  - a semester charge, generated from an active FeeStructure (it has a semester and a structure item);
    //  - a service charge (certificate, transcript...), created for one request (it has a ServiceReference).
    // Description and Amount are snapshots: changing the fee catalog later never changes a charge.
    public sealed class StudentCharge : AggregateRoot
    {
        public const int MaxDescriptionLength = 150;
        public const int MaxReferenceLength = 100;

        private StudentCharge()
        {
        }

        public Guid StudentId { get; private set; }
        public Guid? AcademicSemesterId { get; private set; }
        public Guid? FeeStructureItemId { get; private set; }
        public Guid FeeDefinitionId { get; private set; }
        public string? ServiceReference { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public Money Amount { get; private set; }
        public DateOnly DueDate { get; private set; }
        public ChargeStatus Status { get; private set; }
        public Guid? InvoiceId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public bool IsService => FeeStructureItemId is null;

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

        // A fee for one service request. The reference is the id of that request in the module that owns it.
        public static Result<StudentCharge> CreateService(Guid studentId, Guid feeDefinitionId, string? description, Money amount, DateOnly dueDate, string? reference, DateTimeOffset now)
        {
            var text = description?.Trim();

            if (studentId == Guid.Empty || feeDefinitionId == Guid.Empty || string.IsNullOrEmpty(text) || text.Length > MaxDescriptionLength)
            {
                return FinanceErrors.ChargeDataInvalid;
            }

            var cleanReference = reference?.Trim();

            if (string.IsNullOrEmpty(cleanReference) || cleanReference.Length > MaxReferenceLength)
            {
                return FinanceErrors.ServiceReferenceInvalid;
            }

            if (!amount.IsPositive)
            {
                return FinanceErrors.AmountMustBePositive;
            }

            return new StudentCharge
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                FeeDefinitionId = feeDefinitionId,
                ServiceReference = cleanReference,
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
