using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class ActiveAutomaticHoldSpecification : Specification<FinancialHold>
    {
        public ActiveAutomaticHoldSpecification(Guid studentId, bool tracking = false)
        {
            SetCriteria(h => h.StudentId == studentId && h.Status == FinancialHoldStatus.Active && h.Reason == FinancialHoldReason.OverdueBalance);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

}
