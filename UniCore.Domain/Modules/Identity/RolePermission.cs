using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Identity
{
    // Composite key (RoleId, PermissionId).
    public sealed class RolePermission
    {
        private RolePermission()
        {
        }

        public Guid RoleId { get; private set; }
        public Guid PermissionId { get; private set; }

        public static Result<RolePermission> Create(Guid roleId, Guid permissionId)
        {
            if (roleId == Guid.Empty || permissionId == Guid.Empty)
            {
                return IdentityErrors.RolePermissionDataInvalid;
            }

            return new RolePermission { RoleId = roleId, PermissionId = permissionId };
        }
    }


}
