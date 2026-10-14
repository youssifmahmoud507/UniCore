using UniCore.Application.Modules.Identity;

namespace UniCore.UnitTests.Modules.Identity
{
    internal sealed class TestUserAccessContext : IUserAccessContext
    {
        public UserAccess? Current { get; private set; }
        public void Set(UserAccess access) => Current = access;
    }

}
