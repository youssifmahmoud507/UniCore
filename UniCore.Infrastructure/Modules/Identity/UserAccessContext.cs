using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class UserAccessContext : IUserAccessContext
    {
        public UserAccess? Current { get; private set; }

        public void Set(UserAccess access) => Current = access;
    }




}
