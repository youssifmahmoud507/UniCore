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
    // Request: Payments.Record. Approve, reject and process: Refunds.Approve. Whoever asked cannot approve.
    // Reserving the amount on the payment and taking it off the invoice is done through the Invoice aggregate,
    // so the invoice RowVersion protects every step against parallel requests.
    public sealed class RefundHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Refund> refunds, IAggregateRepository<Invoice> invoices, IRepository<Payment> payments, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Refund> _refunds = refunds;
        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IRepository<Payment> _payments = payments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        private Task AuditAsync(string action, Guid entityId, RequestInfo request, string? oldValues, string? newValues, CancellationToken cancellationToken) => _audit.WriteAsync(
            new AuditEntry(action, _user.UserId, "Refund", entityId.ToString(), request.IpAddress, request.UserAgent, oldValues, newValues),
            cancellationToken);

        private async Task<Result<Refund>> LoadRefundAsync(Guid id, CancellationToken cancellationToken)
        {
            var refund = await _refunds.FirstOrDefaultAsync(new RefundByIdSpecification(id, tracking: true), cancellationToken);

            return refund is null ? FinanceErrors.RefundNotFound : refund;
        }

        private async Task<Result<Invoice>> LoadInvoiceAsync(Guid id, CancellationToken cancellationToken)
        {
            var invoice = await _invoices.FirstOrDefaultAsync(new InvoiceByIdSpecification(id, tracking: true), cancellationToken);

            return invoice is null ? FinanceErrors.InvoiceNotFound : invoice;
        }

        public async Task<Result<RefundDto>> RequestAsync(RequestRefundCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Payments.Record, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RefundDto>.Fail(auth.Errors);
            }

            if (command.PaymentId is not { } paymentId || paymentId == Guid.Empty)
            {
                return FinanceErrors.RefundDataInvalid;
            }

            if (command.Amount is not { } rawAmount)
            {
                return FinanceErrors.AmountRequired;
            }

            var parsed = Money.Of(rawAmount);
            if (!parsed.TryGetValue(out var amount))
            {
                return Result<RefundDto>.Fail(parsed.Errors);
            }

            var payment = await _payments.FirstOrDefaultAsync(new PaymentByIdSpecification(paymentId), cancellationToken);
            if (payment is null)
            {
                return FinanceErrors.PaymentNotFound;
            }

            var loadedInvoice = await LoadInvoiceAsync(payment.InvoiceId, cancellationToken);
            if (!loadedInvoice.TryGetValue(out var invoice))
            {
                return Result<RefundDto>.Fail(loadedInvoice.Errors);
            }

            var now = _clock.UtcNow;

            var reserved = invoice.ReserveRefund(paymentId, amount, now);
            if (reserved.IsFailure)
            {
                return Result<RefundDto>.Fail(reserved.Errors);
            }

            var created = Refund.Request(paymentId, invoice.Id, invoice.StudentId, amount, command.Reason, _user.UserId, now);
            if (!created.TryGetValue(out var refund))
            {
                return Result<RefundDto>.Fail(created.Errors);
            }

            _refunds.Add(refund);

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                // CONCURRENCY_CONFLICT: someone else changed the invoice at the same time. The client can try again.
                return Result<RefundDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.RefundRequested, refund.Id, request, null, $"{invoice.InvoiceNumber} {refund.Amount}", cancellationToken);

            return ReductionsMapper.ToDto(refund);
        }

        public async Task<Result<RefundDto>> ApproveAsync(Guid refundId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Refunds.Approve, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RefundDto>.Fail(auth.Errors);
            }

            var loaded = await LoadRefundAsync(refundId, cancellationToken);
            if (!loaded.TryGetValue(out var refund))
            {
                return Result<RefundDto>.Fail(loaded.Errors);
            }

            var approved = refund.Approve(_user.UserId, _clock.UtcNow);
            if (approved.IsFailure)
            {
                return Result<RefundDto>.Fail(approved.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<RefundDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.RefundApproved, refund.Id, request, RefundStatus.Requested.ToString(), RefundStatus.Approved.ToString(), cancellationToken);

            return ReductionsMapper.ToDto(refund);
        }

        // A rejected refund gives its reserved amount back to the payment.
        public async Task<Result<RefundDto>> RejectAsync(Guid refundId, string? reason, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Refunds.Approve, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RefundDto>.Fail(auth.Errors);
            }

            var loaded = await LoadRefundAsync(refundId, cancellationToken);
            if (!loaded.TryGetValue(out var refund))
            {
                return Result<RefundDto>.Fail(loaded.Errors);
            }

            var oldStatus = refund.Status;
            var now = _clock.UtcNow;

            var rejected = refund.Reject(_user.UserId, reason, now);
            if (rejected.IsFailure)
            {
                return Result<RefundDto>.Fail(rejected.Errors);
            }

            var loadedInvoice = await LoadInvoiceAsync(refund.InvoiceId, cancellationToken);
            if (!loadedInvoice.TryGetValue(out var invoice))
            {
                return Result<RefundDto>.Fail(loadedInvoice.Errors);
            }

            var released = invoice.ReleaseRefund(refund.PaymentId, refund.Amount, now);
            if (released.IsFailure)
            {
                return Result<RefundDto>.Fail(released.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<RefundDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.RefundRejected, refund.Id, request, oldStatus.ToString(), RefundStatus.Rejected.ToString(), cancellationToken);

            return ReductionsMapper.ToDto(refund);
        }

        // The money has been given back: the invoice paid amount goes down and its balance goes up.
        public async Task<Result<RefundDto>> ProcessAsync(Guid refundId, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Refunds.Approve, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RefundDto>.Fail(auth.Errors);
            }

            var loaded = await LoadRefundAsync(refundId, cancellationToken);
            if (!loaded.TryGetValue(out var refund))
            {
                return Result<RefundDto>.Fail(loaded.Errors);
            }

            var now = _clock.UtcNow;

            var processed = refund.MarkProcessed(_user.UserId, now);
            if (processed.IsFailure)
            {
                return Result<RefundDto>.Fail(processed.Errors);
            }

            var loadedInvoice = await LoadInvoiceAsync(refund.InvoiceId, cancellationToken);
            if (!loadedInvoice.TryGetValue(out var invoice))
            {
                return Result<RefundDto>.Fail(loadedInvoice.Errors);
            }

            var applied = invoice.ApplyRefund(refund.Id, refund.PaymentId, refund.Amount, now);
            if (applied.IsFailure)
            {
                return Result<RefundDto>.Fail(applied.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result<RefundDto>.Fail(saved.Errors);
            }

            await AuditAsync(AuditActions.RefundProcessed, refund.Id, request, RefundStatus.Approved.ToString(), $"{RefundStatus.Processed} {refund.Amount}", cancellationToken);

            return ReductionsMapper.ToDto(refund);
        }

        public async Task<Result<PagedResult<RefundDto>>> ListAsync(PaginationQuery query, RefundStatus? status, Guid? studentId, Guid? invoiceId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PagedResult<RefundDto>>.Fail(auth.Errors);
            }

            var specification = new RefundsListSpecification(query, status, studentId, invoiceId);

            var items = await _refunds.ListAsync(specification, cancellationToken);
            var total = await _refunds.CountAsync(specification, cancellationToken);

            return PagedResult<RefundDto>.Create(items.Select(r => ReductionsMapper.ToDto(r)).ToList(), query, total);
        }

        public async Task<Result<RefundDto>> GetAsync(Guid refundId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<RefundDto>.Fail(auth.Errors);
            }

            var refund = await _refunds.FirstOrDefaultAsync(new RefundByIdSpecification(refundId), cancellationToken);

            return refund is null ? FinanceErrors.RefundNotFound : ReductionsMapper.ToDto(refund);
        }
    }






}
