using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Finance
{
    public interface IFinancialEligibilityService
    {
        // True when an active financial hold blocks registration or graduation for this student.
        Task<Result<bool>> HasBlockingHoldAsync(Guid studentId, CancellationToken cancellationToken = default);

        Task<Result<FinancialBalance>> GetBalanceAsync(Guid studentId, CancellationToken cancellationToken = default);
    }

}
