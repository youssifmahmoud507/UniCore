using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class ActiveCampusesOfUniversitySpecification : Specification<Campus>
    {
        public ActiveCampusesOfUniversitySpecification(Guid universityId)
        {
            SetCriteria(c => c.UniversityId == universityId && c.IsActive);
        }
    }
}
