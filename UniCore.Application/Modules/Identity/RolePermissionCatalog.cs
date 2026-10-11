using UniCore.Application.Common.Repositories;
using UniCore.Application.Modules.Identity.Specifications;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Application.Modules.Identity
{
    // The permission codes a role holds. SuperAdmin implicitly holds all of them.
    public sealed class RolePermissionCatalog(IRepository<Permission> permissions, IRepository<RolePermission> rolePermissions)
    {
        private readonly IRepository<Permission> _permissions = permissions;
        private readonly IRepository<RolePermission> _rolePermissions = rolePermissions;

        public async Task<IReadOnlyList<string>> GetCodesAsync(Guid roleId, string roleName, CancellationToken cancellationToken = default)
        {
            if (roleName == RoleNames.SuperAdmin)
            {
                return Permissions.AllCodes.Order(StringComparer.Ordinal).ToList();
            }

            var links = await _rolePermissions.ListAsync(new RolePermissionsByRoleSpecification(roleId), cancellationToken);
            var all = await _permissions.ListAsync(new AllPermissionsSpecification(), cancellationToken);

            var codeById = all.ToDictionary(p => p.Id, p => p.Code);

            return [.. links.Select(link => codeById.TryGetValue(link.PermissionId, out var code) ? code : null).OfType<string>().Order(StringComparer.Ordinal)];
        }
    }



}
