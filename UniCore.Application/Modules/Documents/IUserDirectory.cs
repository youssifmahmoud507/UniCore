namespace UniCore.Application.Modules.Documents
{
    // Used to check that a person who is given access exists and can sign in.
    public interface IUserDirectory
    {
        Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    }


}
