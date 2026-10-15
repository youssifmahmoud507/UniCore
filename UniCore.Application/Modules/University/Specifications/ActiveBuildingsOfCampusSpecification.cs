using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    public sealed class ActiveBuildingsOfCampusSpecification : Specification<Building>
    {
        public ActiveBuildingsOfCampusSpecification(Guid campusId)
        {
            SetCriteria(b => b.CampusId == campusId && b.IsActive);
        }
    }
}
