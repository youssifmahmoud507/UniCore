using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Events;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Outbox;

namespace UniCore.Infrastructure.Modules.Outbox
{
    public sealed class DomainEventOutboxHandler<TEvent> : IOutboxMessageHandler where TEvent : class, IDomainEvent
    {
        public string MessageType => OutboxMessageSerializer.TypeNameOf(typeof(TEvent));

        public bool RedactPayloadWhenDone => false;

        public async Task<Result> HandleAsync(string payload, IServiceProvider services, CancellationToken cancellationToken)
        {
            TEvent? domainEvent;

            try
            {
                domainEvent = JsonSerializer.Deserialize<TEvent>(payload, OutboxMessageSerializer.Options);
            }
            catch (JsonException)
            {
                return Result.Fail(OutboxErrors.PayloadInvalid);
            }

            if (domainEvent is null)
            {
                return Result.Fail(OutboxErrors.PayloadInvalid);
            }

            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            {
                var result = await handler.HandleAsync(domainEvent, cancellationToken);

                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Result.Ok();
        }
    }



}
