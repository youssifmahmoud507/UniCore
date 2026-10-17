using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Finance
{
    public sealed class InvoiceHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Invoice> invoices, IAggregateRepository<StudentCharge> charges, IAggregateRepository<StudentScholarship> awards, IAggregateRepository<Scholarship> scholarships, IAggregateRepository<Discount> discounts, IInvoiceNumberGenerator numbers, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);
        private const int MaxReasonLength = 200;
        private const int MaxDiscountsPerInvoice = 5;

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IAggregateRepository<StudentCharge> _charges = charges;
        private readonly IAggregateRepository<StudentScholarship> _awards = awards;
        private readonly IAggregateRepository<Scholarship> _scholarships = scholarships;
        private readonly IAggregateRepository<Discount> _discounts = discounts;
        private readonly IInvoiceNumberGenerator _numbers = numbers;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        // One invoice for all the pending charges of the student. The charges and the invoice are saved in ONE
        // transaction. Two parallel calls cannot both win: the charges carry a RowVersion and each charge can
        // appear on only one invoice line (unique index).
        public async Task<Result<InvoiceDto>> IssueAsync(IssueInvoiceCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Invoices.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<InvoiceDto>.Fail(auth.Errors);
            }

            if (command.StudentId is not { } studentId || studentId == Guid.Empty
                || (command.AcademicSemesterId is { } semesterId && semesterId == Guid.Empty))
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            var today = _clock.UtcToday;

            if (command.DueDate is { } requested && requested < today)
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            var pending = await _charges.ListAsync(new PendingChargesForStudentSpecification(studentId, command.AcademicSemesterId), cancellationToken);

            if (pending.Count == 0)
            {
                return FinanceErrors.NoPendingCharges;
            }

            if (pending.Count > Invoice.MaxLines)
            {
                return FinanceErrors.InvoiceTooManyLines;
            }

            var reductionResult = await BuildReductionsAsync(studentId, today, command.DiscountIds, cancellationToken);
            if (!reductionResult.TryGetValue(out var reductionInputs))
            {
                return Result<InvoiceDto>.Fail(reductionResult.Errors);
            }

            var latest = pending.Max(c => c.DueDate);
            var dueDate = command.DueDate ?? (latest < today ? today : latest);

            var number = await _numbers.NextAsync(today.Year, cancellationToken);
            if (!number.TryGetValue(out var invoiceNumber))
            {
                return Result<InvoiceDto>.Fail(number.Errors);
            }

            var lines = pending.Select(c => new InvoiceLineInput(c.Description, 1, c.Amount, c.Id)).ToList();

            var now = _clock.UtcNow;

            var issued = Invoice.Issue(studentId, invoiceNumber, today, dueDate, lines, now, reductionInputs);
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
                new AuditEntry(AuditActions.InvoiceIssued, _user.UserId, "Invoice", invoice.Id.ToString(), request.IpAddress, request.UserAgent, null, $"{invoice.InvoiceNumber} total {invoice.TotalAmount} discount {invoice.DiscountAmount} lines {invoice.Lines.Count}"),
                cancellationToken);

            return InvoicingMapper.ToDto(invoice);
        }

        // Cancels an invoice nobody has paid anything on, and the charges on it. All in one transaction.
        public async Task<Result<InvoiceDto>> CancelAsync(Guid invoiceId, string? reason, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Invoices.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<InvoiceDto>.Fail(auth.Errors);
            }

            var cleanReason = reason?.Trim();
            if (string.IsNullOrEmpty(cleanReason))
            {
                cleanReason = null;
            }
            else if (cleanReason.Length > MaxReasonLength)
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            var invoice = await _invoices.FirstOrDefaultAsync(new InvoiceByIdSpecification(invoiceId, tracking: true), cancellationToken);
            if (invoice is null)
            {
                return FinanceErrors.InvoiceNotFound;
            }

            var now = _clock.UtcNow;

            var cancelled = invoice.Cancel(now);
            if (cancelled.IsFailure)
            {
                return Result<InvoiceDto>.Fail(cancelled.Errors);
            }

            var charges = await _charges.ListAsync(new ChargesForInvoiceSpecification(invoice.Id), cancellationToken);

            foreach (var charge in charges)
            {
                var released = charge.CancelWithInvoice(invoice.Id, now);
                if (released.IsFailure)
                {
                    return Result<InvoiceDto>.Fail(released.Errors);
                }
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<InvoiceDto>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.InvoiceCancelled, _user.UserId, "Invoice", invoice.Id.ToString(), request.IpAddress, request.UserAgent, InvoiceStatus.Issued.ToString(), cleanReason is null ? invoice.InvoiceNumber : $"{invoice.InvoiceNumber} reason: {cleanReason}"),
                cancellationToken);

            return InvoicingMapper.ToDto(invoice);
        }

        public async Task<Result<PagedResult<InvoiceSummaryDto>>> ListAsync(PaginationQuery query, Guid? studentId, InvoiceStatus? status, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<InvoiceSummaryDto>>.Fail(auth.Errors);
            }

            var specification = new InvoicesListSpecification(query, studentId, status);

            var items = await _invoices.ListAsync(specification, cancellationToken);
            var total = await _invoices.CountAsync(specification, cancellationToken);

            return PagedResult<InvoiceSummaryDto>.Create(items.Select(InvoicingMapper.ToSummary).ToList(), query, total);
        }

        public async Task<Result<InvoiceDto>> GetAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<InvoiceDto>.Fail(auth.Errors);
            }

            var invoice = await _invoices.FirstOrDefaultAsync(new InvoiceByIdSpecification(invoiceId), cancellationToken);

            return invoice is null ? FinanceErrors.InvoiceNotFound : InvoicingMapper.ToDto(invoice);
        }

        // 1. The scholarships the student holds on the invoice date apply by themselves.
        // 2. Discounts are chosen by the person issuing the invoice. They must exist, be active and be valid today.
        private async Task<Result<List<ReductionInput>>> BuildReductionsAsync(Guid studentId, DateOnly today, IReadOnlyList<Guid>? discountIds, CancellationToken cancellationToken)
        {
            var inputs = new List<ReductionInput>();

            var awards = await _awards.ListAsync(new ActiveAwardsValidOnSpecification(studentId, today), cancellationToken);

            if (awards.Count > 0)
            {
                var ids = awards.Select(a => a.ScholarshipId).Distinct().ToList();
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
