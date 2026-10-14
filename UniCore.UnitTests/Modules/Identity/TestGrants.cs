using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Identity;
using UniCore.Domain.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    internal static class TestGrants
    {
        public static RoleGrant SuperAdmin() => new(RoleNames.SuperAdmin,new HashSet<string>(Permissions.AllCodes, StringComparer.Ordinal),[new ScopeGrant(ScopeType.Global, null)]);
        public static RoleGrant Role(string name, IEnumerable<string> permissions, params ScopeGrant[] scopes) => new(name, permissions.ToHashSet(StringComparer.Ordinal), scopes);
    }

}
