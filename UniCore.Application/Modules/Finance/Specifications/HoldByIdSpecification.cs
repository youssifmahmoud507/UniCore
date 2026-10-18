using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class HoldByIdSpecification : Specification<FinancialHold>
    {
        public HoldByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(h => h.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }

}
