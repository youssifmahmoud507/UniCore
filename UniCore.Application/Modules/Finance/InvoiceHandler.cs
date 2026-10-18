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
    public sealed class InvoiceHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Invoice> invoices, IAggregateRepository<StudentCharge> charges, InvoiceIssuer issuer, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);
        private const int MaxReasonLength = 200;

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IAggregateRepository<StudentCharge> _charges = charges;
        private readonly InvoiceIssuer _issuer = issuer;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        public async Task<Result<InvoiceDto>> IssueAsync(IssueInvoiceCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Invoices.Manage, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<InvoiceDto>.Fail(auth.Errors);
            }

            if (command.StudentId is not { } studentId || studentId == Guid.Empty
                || (command.AcademicSemesterId is { } semesterId && semesterId == Guid.Empty)
                || (command.ChargeIds is { } chargeIds && chargeIds.Contains(Guid.Empty)))
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            return await _issuer.IssueAsync(new InvoiceIssueRequest(studentId, command.AcademicSemesterId, command.DueDate, command.DiscountIds, command.ChargeIds), _user.UserId, request, cancellationToken);
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
    }


}
