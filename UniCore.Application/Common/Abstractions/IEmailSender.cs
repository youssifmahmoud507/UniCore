using UniCore.Domain.Common.Results;

namespace UniCore.Application.Common.Abstractions
{
    public interface IEmailSender
    {
        // SMTP failures come back as EXTERNAL_SERVICE_UNAVAILABLE, never as exceptions.
        Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }
}
