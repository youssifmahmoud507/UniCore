using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using UniCore.Domain.Common.Events;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Outbox;

namespace UniCore.Infrastructure.Modules.Outbox
{
    public static class OutboxMessageSerializer
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

        // The message type of a domain event is its full type name.
        public static string TypeNameOf(Type type) => type.FullName ?? type.Name;

        public static Result<OutboxMessage> FromDomainEvent(IDomainEvent domainEvent, DateTimeOffset now) => FromObject(TypeNameOf(domainEvent.GetType()), domainEvent, domainEvent.OccurredAt, now);

        public static Result<OutboxMessage> FromObject(string type, object value, DateTimeOffset occurredAt, DateTimeOffset now, DateTimeOffset? notBefore = null)
        {
            string payload;

            try
            {
                payload = JsonSerializer.Serialize(value, value.GetType(), Options);
            }
            catch (NotSupportedException)
            {
                return OutboxErrors.SerializationFailed;
            }

            return OutboxMessage.Create(type, payload, occurredAt, now, notBefore);
        }
    }



}
