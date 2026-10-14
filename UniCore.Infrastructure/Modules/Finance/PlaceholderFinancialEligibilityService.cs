using UniCore.Application.Modules.Finance;
using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Finance
{
    // TEMPORARY, so Developer 1 can code against the contract today: nobody has a hold and every balance is zero.
    // Replaced by the real service in batch F5 (invoices, payments and holds).
    public sealed class PlaceholderFinancialEligibilityService : IFinancialEligibilityService
    {
        public Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, CancellationToken cancellationToken = default) => Task.FromResult(Result<bool>.Ok(false));
        public Task<Result<FinancialBalance>> GetBalanceAsync(Guid studentId, CancellationToken cancellationToken = default) => Task.FromResult(Result<FinancialBalance>.Ok(new FinancialBalance(studentId, 0m, 0m, 0m)));
    }

}
