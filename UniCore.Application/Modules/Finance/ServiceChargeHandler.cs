using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Finance
{
    // The staff door to IStudentChargeService, with the option to invoice just this charge.
    public sealed class ServiceChargeHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IStudentChargeService service, InvoiceIssuer issuer, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IStudentChargeService _service = service;
        private readonly InvoiceIssuer _issuer = issuer;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<ServiceChargeOutcome>> CreateAsync(CreateServiceChargeCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Invoices.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<ServiceChargeOutcome>.Fail(auth.Errors);
            }

            if (command.StudentId is not { } studentId || studentId == Guid.Empty)
            {
                return FinanceErrors.ChargeDataInvalid;
            }

            var charged = await _service.ChargeAsync(new ServiceChargeRequest(studentId, command.FeeCode, command.Reference, command.DueDate, command.Amount), cancellationToken);
            if (!charged.TryGetValue(out var result))
            {
                return Result<ServiceChargeOutcome>.Fail(charged.Errors);
            }

            InvoiceDto? invoice = null;

            // If this fails the charge stays pending. Calling again with the same reference retries the invoice only.
            if (command.IssueInvoice && result.Charge.Status == ChargeStatus.Pending)
            {
                var issued = await _issuer.IssueAsync(new InvoiceIssueRequest(studentId, null, null, null, [result.Charge.Id]), _user.UserId, request, cancellationToken);
                if (!issued.TryGetValue(out var issuedInvoice))
                {
                    return Result<ServiceChargeOutcome>.Fail(issued.Errors);
                }

                invoice = issuedInvoice;
            }

            if (result.Created)
            {
                await _audit.WriteAsync(
                    new AuditEntry(AuditActions.ServiceChargeCreated, _user.UserId, "StudentCharge", result.Charge.Id.ToString(), request.IpAddress, request.UserAgent, null, $"{command.FeeCode?.Trim()} {result.Charge.Amount} ref {result.Charge.ServiceReference}"),
                    cancellationToken);
            }

            return new ServiceChargeOutcome(result.Charge, result.Created, invoice);
        }
    }

}
