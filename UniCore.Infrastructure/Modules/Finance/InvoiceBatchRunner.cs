using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Infrastructure.Modules.Finance
{
    // Every student gets its own DI scope, so its own DbContext: a failed save for one student leaves nothing
    // behind that could spoil the next one. Scholarships apply by themselves; discounts are not applied in bulk.
    public sealed class InvoiceBatchRunner(IServiceScopeFactory scopes) : IInvoiceBatchRunner
    {
        private readonly IServiceScopeFactory _scopes = scopes;

        public async Task<Result<BulkInvoiceResult>> RunAsync(Guid academicSemesterId, DateOnly? dueDate, CancellationToken cancellationToken = default)
        {
            List<Guid> studentIds;

            using (var scope = _scopes.CreateScope())
            {
                var charges = scope.ServiceProvider.GetRequiredService<IAggregateRepository<StudentCharge>>();
                var pending = await charges.ListAsync(new PendingChargesForSemesterSpecification(academicSemesterId), cancellationToken);

                studentIds = [.. pending.Select(c => c.StudentId).Distinct()];
            }

            var invoiced = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var studentId in studentIds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                using var scope = _scopes.CreateScope();
                var issuer = scope.ServiceProvider.GetRequiredService<InvoiceIssuer>();

                var issued = await issuer.IssueAsync(new InvoiceIssueRequest(studentId, academicSemesterId, dueDate, null, null), null, null, cancellationToken);

                if (issued.IsSuccess)
                {
                    invoiced++;
                }
                else if (issued.Error?.Code == FinanceErrors.NoPendingCharges.Code)
                {
                    skipped++;
                }
                else
                {
                    failed++;
                }
            }

            return new BulkInvoiceResult(invoiced, skipped, failed);
        }
    }

}
