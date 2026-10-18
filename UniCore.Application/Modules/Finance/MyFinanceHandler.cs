using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Pagination;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Application.Modules.Students.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;
using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Finance
{
    // A student reading their own money. The student is found from the logged-in user; the check is ownership.
    public sealed class MyFinanceHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Student> students, IAggregateRepository<Invoice> invoices, IRepository<Payment> payments, IFinancialEligibilityService eligibility)
    {
        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Student> _students = students;
        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IRepository<Payment> _payments = payments;
        private readonly IFinancialEligibilityService _eligibility = eligibility;

        private async Task<Result<Student>> MyStudentAsync(CancellationToken cancellationToken)
        {
            if (!_user.IsAuthenticated || _user.UserId is not { } userId)
            {
                return Error.Unauthorized();
            }

            var student = await _students.FirstOrDefaultAsync(new StudentByUserSpecification(userId), cancellationToken);
            if (student is null)
            {
                return StudentErrors.NotFound;
            }

            var auth = await _authorizer.AuthorizeAsync(Permissions.Finance.Read, ResourceContext.OwnedBy(userId), cancellationToken);
            if (auth.IsFailure)
            {
                return Result<Student>.Fail(auth.Errors);
            }

            return student;
        }

        public async Task<Result<MyFinanceDto>> GetFinanceAsync(CancellationToken cancellationToken = default)
        {
            var mine = await MyStudentAsync(cancellationToken);
            if (!mine.TryGetValue(out var student))
            {
                return Result<MyFinanceDto>.Fail(mine.Errors);
            }

            var balance = await _eligibility.GetBalanceAsync(student.Id, cancellationToken);
            if (!balance.TryGetValue(out var money))
            {
                return Result<MyFinanceDto>.Fail(balance.Errors);
            }

            var holds = await _eligibility.GetActiveHoldsAsync(student.Id, cancellationToken);
            if (!holds.TryGetValue(out var active))
            {
                return Result<MyFinanceDto>.Fail(holds.Errors);
            }

            return new MyFinanceDto(
                money,
                active.Count,
                active.Any(h => h.Areas.Contains(FinancialHoldArea.Registration)),
                active.Any(h => h.Areas.Contains(FinancialHoldArea.Graduation)),
                active.Any(h => h.Areas.Contains(FinancialHoldArea.Services)));
        }

        public async Task<Result<PagedResult<InvoiceSummaryDto>>> ListInvoicesAsync(PaginationQuery query, InvoiceStatus? status, CancellationToken cancellationToken = default)
        {
            var mine = await MyStudentAsync(cancellationToken);
            if (!mine.TryGetValue(out var student))
            {
                return Result<PagedResult<InvoiceSummaryDto>>.Fail(mine.Errors);
            }

            var specification = new InvoicesListSpecification(query, student.Id, status);

            var items = await _invoices.ListAsync(specification, cancellationToken);
            var total = await _invoices.CountAsync(specification, cancellationToken);

            return PagedResult<InvoiceSummaryDto>.Create(items.Select(InvoicingMapper.ToSummary).ToList(), query, total);
        }

        // Someone else's invoice looks exactly like an invoice that does not exist.
        public async Task<Result<InvoiceDto>> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var mine = await MyStudentAsync(cancellationToken);
            if (!mine.TryGetValue(out var student))
            {
                return Result<InvoiceDto>.Fail(mine.Errors);
            }

            var invoice = await _invoices.FirstOrDefaultAsync(new InvoiceByIdSpecification(invoiceId), cancellationToken);
            if (invoice is null || invoice.StudentId != student.Id)
            {
                return FinanceErrors.InvoiceNotFound;
            }

            var dto = InvoicingMapper.ToDto(invoice);

            return dto with { Payments = [.. dto.Payments.Select(p => p with { RecordedByUserId = null })] };
        }

        public async Task<Result<PagedResult<MyPaymentDto>>> ListPaymentsAsync(PaginationQuery query, CancellationToken cancellationToken = default)
        {
            var mine = await MyStudentAsync(cancellationToken);
            if (!mine.TryGetValue(out var student))
            {
                return Result<PagedResult<MyPaymentDto>>.Fail(mine.Errors);
            }

            // Only invoices that are not cancelled can have payments.
            var invoiceList = await _invoices.ListAsync(new InvoicesForBalanceSpecification(student.Id), cancellationToken);
            var invoiceIds = invoiceList.Select(i => i.Id).ToArray();

            if (invoiceIds.Length == 0)
            {
                return PagedResult<MyPaymentDto>.Create(new List<MyPaymentDto>(), query, 0);
            }

            var specification = new PaymentsForInvoicesSpecification(invoiceIds, query);

            var items = await _payments.ListAsync(specification, cancellationToken);
            var total = await _payments.CountAsync(specification, cancellationToken);

            var dtos = items.Select(p => new MyPaymentDto(p.Id, p.InvoiceId, p.Amount.Amount, p.PaymentDate, p.Method, p.Reference, p.RefundedAmount.Amount, p.CreatedAt)).ToList();

            return PagedResult<MyPaymentDto>.Create(dtos, query, total);
        }
    }
}
