using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class InvoiceByIdSpecification : Specification<Invoice>
    {
        public InvoiceByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(i => i.Id == id);
            AddInclude("Lines");
            AddInclude("Payments");
            AddInclude("Reductions");

            if (tracking)
            {
                UseTracking();
            }
        }
    }

}
