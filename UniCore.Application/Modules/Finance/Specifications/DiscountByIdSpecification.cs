using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // ---------- discounts ----------

    public sealed class DiscountByIdSpecification : Specification<Discount>
    {
        public DiscountByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(d => d.Id == id);

            if (tracking)
            {
                UseTracking();
            }
        }
    }






}
