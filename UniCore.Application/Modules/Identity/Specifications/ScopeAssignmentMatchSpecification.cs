using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class ScopeAssignmentMatchSpecification : Specification<ScopeAssignment>
    {
        public ScopeAssignmentMatchSpecification(Guid userId, Guid roleId, ScopeType scopeType, Guid? scopeId)
        {
            SetCriteria(s => s.UserId == userId && s.RoleId == roleId && s.ScopeType == scopeType && s.ScopeId == scopeId);
        }
    }

}
