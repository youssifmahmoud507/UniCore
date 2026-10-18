using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class ScholarshipByCodeSpecification : Specification<Scholarship>
    {
        public ScholarshipByCodeSpecification(string code)
        {
            SetCriteria(s => s.Code == code);
        }
    }






}
