using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class ScopeAssignmentByIdSpecification : Specification<ScopeAssignment>
    {
        public ScopeAssignmentByIdSpecification(Guid id)
        {
            SetCriteria(s => s.Id == id);
            UseTracking();
        }
    }

}
