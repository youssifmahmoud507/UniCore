using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // The code must already be normalized (upper case). Every university, so the caller can see an ambiguous code.
    public sealed class FeeDefinitionsByCodeSpecification : Specification<FeeDefinition>
    {
        public FeeDefinitionsByCodeSpecification(string code)
        {
            SetCriteria(d => d.Code == code);
        }
    }


}
