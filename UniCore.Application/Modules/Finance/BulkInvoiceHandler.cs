using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Audit;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Finance
{
    public sealed class BulkInvoiceHandler(IPermissionAuthorizer authorizer, ICurrentUser user, IInvoiceBatchRunner runner, IClock clock, IAuditWriter audit)
    {
        private static readonly ResourceContext GlobalResource = ResourceContext.InScope(ScopeType.Global, Guid.Empty);

        private readonly IPermissionAuthorizer _authorizer = authorizer;
        private readonly ICurrentUser _user = user;
        private readonly IInvoiceBatchRunner _runner = runner;
        private readonly IClock _clock = clock;
        private readonly IAuditWriter _audit = audit;

        public async Task<Result<BulkInvoiceResult>> IssueSemesterAsync(IssueSemesterInvoicesCommand command, RequestInfo request, CancellationToken cancellationToken = default)
        {
            var auth = await _authorizer.AuthorizeAsync(Permissions.Invoices.Manage, GlobalResource, cancellationToken);
            if (auth.IsFailure)
            {
                return Result<BulkInvoiceResult>.Fail(auth.Errors);
            }

            if (command.AcademicSemesterId is not { } semesterId || semesterId == Guid.Empty
                || (command.DueDate is { } due && due < _clock.UtcToday))
            {
                return FinanceErrors.InvoiceDataInvalid;
            }

            var ran = await _runner.RunAsync(semesterId, command.DueDate, cancellationToken);
            if (!ran.TryGetValue(out var outcome))
            {
                return Result<BulkInvoiceResult>.Fail(ran.Errors);
            }

            await _audit.WriteAsync(
                new AuditEntry(AuditActions.InvoicesBulkIssued, _user.UserId, "AcademicSemester", semesterId.ToString(), request.IpAddress, request.UserAgent, null, $"invoiced {outcome.StudentsInvoiced}, skipped {outcome.StudentsSkipped}, failed {outcome.StudentsFailed}"),
                cancellationToken);

            return outcome;
        }
    }

}
