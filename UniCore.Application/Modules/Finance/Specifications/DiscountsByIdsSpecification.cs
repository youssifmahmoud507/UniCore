using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class DiscountsByIdsSpecification : Specification<Discount>
    {
        public DiscountsByIdsSpecification(IReadOnlyCollection<Guid> ids)
        {
            var list = ids.ToList();
            SetCriteria(d => list.Contains(d.Id));
        }
    }






}
