using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Outbox;

namespace UniCore.Infrastructure.Modules.Outbox
{
    public sealed class EmailOutboxHandler : IOutboxMessageHandler
    {
        public const string EmailMessageType = "email.send";

        public string MessageType => EmailMessageType;

        // The payload holds the whole email (OTP codes included), so it is wiped as soon as the message is done.
        public bool RedactPayloadWhenDone => true;

        public async Task<Result> HandleAsync(string payload, IServiceProvider services, CancellationToken cancellationToken)
        {
            EmailMessage? email;

            try
            {
                email = JsonSerializer.Deserialize<EmailMessage>(payload, OutboxMessageSerializer.Options);
            }
            catch (JsonException)
            {
                return Result.Fail(OutboxErrors.PayloadInvalid);
            }

            if (email is null)
            {
                return Result.Fail(OutboxErrors.PayloadInvalid);
            }

            var sender = services.GetRequiredService<IEmailSender>();

            return await sender.SendAsync(email, cancellationToken);
        }
    }



}
