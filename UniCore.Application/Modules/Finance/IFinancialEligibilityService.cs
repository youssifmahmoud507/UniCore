using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance
{
    // Contract for Developer 1 (Registration, Graduation, Services).
    // Registration and Graduation return FinanceErrors.FinancialHold when a hold blocks them.
    public interface IFinancialEligibilityService
    {
        // True when the student has any active hold, whatever it blocks.
        Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, CancellationToken cancellationToken = default);

        // True when an active hold blocks this area (Registration, Graduation or Services).
        Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, FinancialHoldArea area, CancellationToken cancellationToken = default);

        Task<Result<IReadOnlyList<FinancialHoldDto>>> GetActiveHoldsAsync(Guid studentId, CancellationToken cancellationToken = default);

        // Charged: invoices that are not cancelled plus charges not yet invoiced. Paid: what was paid, net of refunds.
        // Outstanding = Charged - Paid (negative would mean the student has credit).
        Task<Result<FinancialBalance>> GetBalanceAsync(Guid studentId, CancellationToken cancellationToken = default);
    }

}
