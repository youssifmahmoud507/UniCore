using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    // The code must already be normalized (upper case).
    public sealed class FeeDefinitionByCodeSpecification : Specification<FeeDefinition>
    {
        public FeeDefinitionByCodeSpecification(Guid universityId, string code)
        {
            SetCriteria(d => d.UniversityId == universityId && d.Code == code);
        }
    }



}
