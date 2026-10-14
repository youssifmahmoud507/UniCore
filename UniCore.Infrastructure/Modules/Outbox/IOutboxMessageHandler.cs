using UniCore.Domain.Common.Results;

namespace UniCore.Infrastructure.Modules.Outbox
{
    // Knows how to process ONE message type. Registered as a singleton.
    public interface IOutboxMessageHandler
    {
        string MessageType { get; }

        // True for messages that carry secrets (an email with an OTP): the payload is wiped when done.
        bool RedactPayloadWhenDone { get; }

        Task<Result> HandleAsync(string payload, IServiceProvider services, CancellationToken cancellationToken);
    }



}
