using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class FeeDefinitionsByIdsSpecification : Specification<FeeDefinition>
    {
        public FeeDefinitionsByIdsSpecification(Guid[] ids)
        {
            SetCriteria(d => ids.Contains(d.Id));
        }
    }

}
