namespace UniCore.Application.Common.Abstractions
{
    public interface IEmailQueue
    {
        // Saves the email in the outbox; a background job delivers it (with retries).
        // Returns false when the email could not be saved.
        Task<bool> TryEnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

}
