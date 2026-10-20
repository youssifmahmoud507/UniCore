namespace UniCore.Application.Modules.Notifications
{
    public interface IUserContactLookup
    {
        // Null when the user has no address or the account is not active.
        Task<string?> GetEmailAsync(Guid userId, CancellationToken cancellationToken = default);
    }


}
