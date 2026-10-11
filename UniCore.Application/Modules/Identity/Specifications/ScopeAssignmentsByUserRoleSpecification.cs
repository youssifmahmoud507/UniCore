using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class ScopeAssignmentsByUserRoleSpecification : Specification<ScopeAssignment>
    {
        public ScopeAssignmentsByUserRoleSpecification(Guid userId, Guid roleId)
        {
            SetCriteria(s => s.UserId == userId && s.RoleId == roleId);
            UseTracking();
        }
    }

}
