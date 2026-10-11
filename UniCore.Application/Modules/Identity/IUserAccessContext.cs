namespace UniCore.Application.Modules.Identity
{
    // Per-request holder, filled once by the access middleware after authentication.
    public interface IUserAccessContext
    {
        UserAccess? Current { get; }
        void Set(UserAccess access);
    }



}
