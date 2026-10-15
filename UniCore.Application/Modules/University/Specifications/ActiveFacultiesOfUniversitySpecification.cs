using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.University;

namespace UniCore.Application.Modules.University.Specifications
{
    // Used to decide whether a university can be deactivated.
    public sealed class ActiveFacultiesOfUniversitySpecification : Specification<Faculty>
    {
        public ActiveFacultiesOfUniversitySpecification(Guid universityId)
        {
            SetCriteria(f => f.UniversityId == universityId && f.IsActive);
        }
    }
}
