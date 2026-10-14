using UniCore.Application.Common.Abstractions;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Finance
{
    public sealed class PaymentHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IAggregateRepository<Invoice> invoices, IRepository<Payment> payments, IUnitOfWork unitOfWork, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IRepository<Payment> _payments = payments;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        private Task<Result> RequireAsync(string permission, CancellationToken cancellationToken) => _authorizer.AuthorizeAsync(permission, GlobalResource, cancellationToken);

        public async Task<Result<PaymentOutcome>> RecordAsync(Guid invoiceId, RecordPaymentCommand command, string? idempotencyKey, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Payments.Record, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<PaymentOutcome>.Fail(auth.Errors);
            }

            var key = Payment.NormalizeKey(idempotencyKey);
            if (key is null)
            {
                return FinanceErrors.IdempotencyKeyInvalid;
            }

            if (command.Amount is not { } rawAmount)
            {
                return FinanceErrors.AmountRequired;
            }

            var parsed = Money.Of(rawAmount);
            if (!parsed.TryGetValue(out var amount))
            {
                return Result<PaymentOutcome>.Fail(parsed.Errors);
            }

            if (command.Method is not { } method)
            {
                return FinanceErrors.PaymentDataInvalid;
            }

            // The same key and the same content again: give back the original answer. This is checked BEFORE the
            // invoice state, so a replay still works after the invoice became fully paid.
            var existing = await _payments.FirstOrDefaultAsync(new PaymentByIdempotencyKeySpecification(key), cancellationToken);
            if (existing is not null)
            {
                return Replay(existing, invoiceId, amount, method, command.Reference);
            }

            var invoice = await _invoices.FirstOrDefaultAsync(new InvoiceByIdSpecification(invoiceId, tracking: true), cancellationToken);
            if (invoice is null)
            {
                return FinanceErrors.InvoiceNotFound;
            }

            var today = _clock.UtcToday;

            var recorded = invoice.RecordPayment(amount, command.PaymentDate ?? today, today, method, command.Reference, key, _user.UserId, _clock.UtcNow);
            if (!recorded.TryGetValue(out var payment))
            {
                return Result<PaymentOutcome>.Fail(recorded.Errors);
            }

            var saved = await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (saved.IsFailure)
            {
                if (saved.Error?.Code == CommonErrors.DuplicateResource.Code)
                {
                    // A parallel request with the same key won the race.
                    var winner = await _payments.FirstOrDefaultAsync(new PaymentByIdempotencyKeySpecification(key), cancellationToken);
                    if (winner is not null)
                    {
                        return Replay(winner, invoiceId, amount, method, command.Reference);
                    }
                }

                // CONCURRENCY_CONFLICT lands here: someone else changed the invoice. The client can retry with the same key.
                return Result<PaymentOutcome>.Fail(saved.Errors);
            }

            await _audit.WriteAsync(new AuditEntry(AuditActions.PaymentRecorded, _user.UserId, "Invoice", invoice.Id.ToString(), request.IpAddress, request.UserAgent, null, $"{invoice.InvoiceNumber} paid {payment.Amount} via {payment.Method}"),cancellationToken);

            return new PaymentOutcome(InvoicingMapper.ToDto(payment), false);
        }

        public async Task<Result<IReadOnlyList<PaymentDto>>> ListForInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            var auth = await RequireAsync(Permissions.Finance.Read, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<IReadOnlyList<PaymentDto>>.Fail(auth.Errors);
            }

            if (!await _invoices.AnyAsync(new InvoiceByIdSpecification(invoiceId), cancellationToken))
            {
                return FinanceErrors.InvoiceNotFound;
            }

            var items = await _payments.ListAsync(new PaymentsForInvoiceSpecification(invoiceId), cancellationToken);

            return Result<IReadOnlyList<PaymentDto>>.Ok([.. items.Select(p => InvoicingMapper.ToDto(p))]);
        }

        private static Result<PaymentOutcome> Replay(Payment existing, Guid invoiceId, Money amount, PaymentMethod method, string? reference)
        {
            var cleanReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();

            var same = existing.InvoiceId == invoiceId && existing.Amount == amount && existing.Method == method && existing.Reference == cleanReference;

            return same ? new PaymentOutcome(InvoicingMapper.ToDto(existing), true) : FinanceErrors.IdempotencyKeyReused;
        }
    }

}
