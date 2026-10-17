using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Finance;

namespace UniCore.Application.Modules.Finance.Specifications
{
    public sealed class ScholarshipsByIdsSpecification : Specification<Scholarship>
    {
        public ScholarshipsByIdsSpecification(IReadOnlyCollection<Guid> ids)
        {
            var list = ids.ToList();
            SetCriteria(s => list.Contains(s.Id));
        }
    }






}
