using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // ---------- refunds and payments ----------

    public sealed class RefundByIdSpecification : Specification<Refund>
    {
        public RefundByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(r => r.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }






}
