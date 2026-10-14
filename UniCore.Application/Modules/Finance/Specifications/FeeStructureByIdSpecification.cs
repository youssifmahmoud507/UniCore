using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class FeeStructureByIdSpecification : Specification<FeeStructure>
    {
        public FeeStructureByIdSpecification(Guid id, bool tracking = false)
        {
            SetCriteria(s => s.Id == id);
            AddInclude("Items");

            if (tracking)
            {
                UseTracking();
            }
        }
    }



}
