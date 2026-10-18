using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // Issues one invoice. No authorization here: the handlers (a person) and the batch runner (the system) call it.
    // The charges and the invoice are saved in ONE transaction. Two parallel calls cannot both win: the charges carry
    // a RowVersion and each charge can be on only one invoice line (unique index).
    public sealed class InvoiceIssuer(IAggregateRepository<Invoice> invoices, IAggregateRepository<StudentCharge> charges, IAggregateRepository<StudentScholarship> awards, IAggregateRepository<Scholarship> scholarships, IAggregateRepository<Discount> discounts, IInvoiceNumberGenerator numbers, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private const int MaxDiscountsPerInvoice = 5;

        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IAggregateRepository<StudentCharge> _charges = charges;
        private readonly IAggregateRepository<StudentScholarship> _awards = awards;
        private readonly IAggregateRepository<Scholarship> _scholarships = scholarships;
        private readonly IAggregateRepository<Discount> _discounts = discounts;
        private readonly IInvoiceNumberGenerator _numbers = numbers;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<InvoiceDto>> IssueAsync(InvoiceIssueRequest request, Guid? actorUserId, RequestInfo? requestInfo, CancellationToken cancellationToken = default)
        {
            var today = _clock.UtcToday;

            if (request.DueDate is { } requested && requested < today)
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            var pending = await _charges.ListAsync(new PendingChargesForStudentSpecification(request.StudentId, request.AcademicSemesterId, request.ChargeIds), cancellationToken);

            if (pending.Count == 0)
            {
                return FinanceErrors.NoPendingCharges;
            }

            // Asked for specific charges: every one of them must be pending and belong to this student.
            if (request.ChargeIds is { Count: > 0 } wanted && pending.Count != wanted.Distinct().Count())
            {
                return FinanceErrors.ChargeNotPending;
            }

            if (pending.Count > Invoice.MaxLines)
            {
                return FinanceErrors.InvoiceTooManyLines;
            }

            var reductionResult = await BuildReductionsAsync(request.StudentId, today, request.DiscountIds, cancellationToken);
            if (!reductionResult.TryGetValue(out var reductionInputs))
            {
                return Result<InvoiceDto>.Fail(reductionResult.Errors);
            }

            var latest = pending.Max(c => c.DueDate);
            var dueDate = request.DueDate ?? (latest < today ? today : latest);

            var number = await _numbers.NextAsync(today.Year, cancellationToken);
            if (!number.TryGetValue(out var invoiceNumber))
            {
                return Result<InvoiceDto>.Fail(number.Errors);
            }

            var lines = pending.Select(c => new InvoiceLineInput(c.Description, 1, c.Amount, c.Id)).ToList();

            var now = _clock.UtcNow;

            var issued = Invoice.Issue(request.StudentId, invoiceNumber, today, dueDate, lines, now, reductionInputs);
            if (!issued.TryGetValue(out var invoice))
            {
                return Result<InvoiceDto>.Fail(issued.Errors);
            }

            foreach (var charge in pending)
            {
                var marked = charge.MarkInvoiced(invoice.Id, now);
                if (marked.IsFailure)
                {
                    return Result<InvoiceDto>.Fail(marked.Errors);
                }
            }

            _invoices.Add(invoice);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<InvoiceDto>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.InvoiceIssued, actorUserId, "Invoice", invoice.Id.ToString(), requestInfo?.IpAddress, requestInfo?.UserAgent, null, $"{invoice.InvoiceNumber} total {invoice.TotalAmount} discount {invoice.DiscountAmount} lines {invoice.Lines.Count}"),
                cancellationToken);

            return InvoicingMapper.ToDto(invoice);
        }

        // 1. The scholarships the student holds on the invoice date apply by themselves.
        // 2. Discounts are chosen by the person issuing the invoice. They must exist, be active and be valid today.
        private async Task<Result<List<ReductionInput>>> BuildReductionsAsync(Guid studentId, DateOnly today, IReadOnlyList<Guid>? discountIds, CancellationToken cancellationToken)
        {
            var inputs = new List<ReductionInput>();

            var awardList = await _awards.ListAsync(new ActiveAwardsValidOnSpecification(studentId, today), cancellationToken);

            if (awardList.Count > 0)
            {
                var ids = awardList.Select(a => a.ScholarshipId).Distinct().ToList();
                var definitions = await _scholarships.ListAsync(new ScholarshipsByIdsSpecification(ids), cancellationToken);

                inputs.AddRange(definitions
                    .Where(s => s.IsActive)
                    .Select(s => new ReductionInput(ReductionKind.Scholarship, s.Id, s.Name, s.Type, s.Value)));
            }

            var requested = discountIds?.Distinct().ToList() ?? [];

            if (requested.Count > MaxDiscountsPerInvoice || requested.Contains(Guid.Empty))
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            if (requested.Count > 0)
            {
                var found = await _discounts.ListAsync(new DiscountsByIdsSpecification(requested), cancellationToken);

                if (found.Count != requested.Count)
                {
                    return FinanceErrors.DiscountNotFound;
                }

                if (found.Any(d => !d.IsValidOn(today)))
                {
                    return FinanceErrors.DiscountNotValid;
                }

                inputs.AddRange(found.Select(d => new ReductionInput(ReductionKind.Discount, d.Id, d.Name, d.Type, d.Value)));
            }

            return inputs;
        }
    }

}
