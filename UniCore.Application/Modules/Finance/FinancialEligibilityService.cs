using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Finance.Specifications;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    public sealed class FinancialEligibilityService(IAggregateRepository<Invoice> invoices, IAggregateRepository<StudentCharge> charges, IAggregateRepository<FinancialHold> holds) : IFinancialEligibilityService
    {
        private readonly IAggregateRepository<Invoice> _invoices = invoices;
        private readonly IAggregateRepository<StudentCharge> _charges = charges;
        private readonly IAggregateRepository<FinancialHold> _holds = holds;

        public async Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var active = await _holds.ListAsync(new ActiveHoldsForStudentSpecification(studentId), cancellationToken);

            return Result<bool>.Ok(active.Count > 0);
        }

        public async Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, FinancialHoldArea area, CancellationToken cancellationToken = default)
        {
            var active = await _holds.ListAsync(new ActiveHoldsForStudentSpecification(studentId), cancellationToken);

            return Result<bool>.Ok(active.Any(h => h.Blocks(area)));
        }

        public async Task<Result<IReadOnlyList<FinancialHoldDto>>> GetActiveHoldsAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var active = await _holds.ListAsync(new ActiveHoldsForStudentSpecification(studentId), cancellationToken);

            return Result<IReadOnlyList<FinancialHoldDto>>.Ok([.. active.Select(h => FinancialHoldMapper.ToDto(h))]);
        }

        public async Task<Result<FinancialBalance>> GetBalanceAsync(Guid studentId, CancellationToken cancellationToken = default)
        {
            var studentInvoices = await _invoices.ListAsync(new InvoicesForBalanceSpecification(studentId), cancellationToken);
            var pending = await _charges.ListAsync(new PendingChargesReadSpecification(studentId), cancellationToken);

            var charged = studentInvoices.Sum(i => i.TotalAmount.Amount) + pending.Sum(c => c.Amount.Amount);
            var paid = studentInvoices.Sum(i => i.PaidAmount.Amount);

            return Result<FinancialBalance>.Ok(new FinancialBalance(studentId, charged, paid, charged - paid));
        }
    }




}
