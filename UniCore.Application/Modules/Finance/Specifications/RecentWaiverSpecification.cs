using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // An automatic hold that a person released since the given time (a waiver).
    public sealed class RecentWaiverSpecification : Specification<FinancialHold>
    {
        public RecentWaiverSpecification(Guid studentId, DateTimeOffset since)
        {
            SetCriteria(h => h.StudentId == studentId
                && h.Reason == FinancialHoldReason.OverdueBalance
                && h.Status == FinancialHoldStatus.Released
                && h.ReleasedByUserId != null
                && h.ReleasedAt >= since);
        }
    }

}
