using UniCore.Application.Common.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity.Specifications
{
    public sealed class RolePermissionsByRoleSpecification : Specification<RolePermission>
    {
        public RolePermissionsByRoleSpecification(Guid roleId)
        {
            SetCriteria(rp => rp.RoleId == roleId);
            UseTracking();
        }
    }

}
