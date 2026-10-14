using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Students;
using UniCore.Domain.Common.Events;
using UniCore.Domain.Modules.Students;

namespace UniCore.Infrastructure.Modules.Outbox
{
    public static class OutboxServiceCollectionExtensions
    {
        public static IServiceCollection AddOutbox(this IServiceCollection services)
        {
            services.AddSingleton<OutboxProcessor>();

            // One message handler per domain event, found by scanning the Domain assembly.
            var eventTypes = typeof(IDomainEvent).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(t));

            foreach (var eventType in eventTypes)
            {
                services.AddSingleton(typeof(IOutboxMessageHandler), typeof(DomainEventOutboxHandler<>).MakeGenericType(eventType));
            }

            // Consumers of domain events.
            services.AddScoped<IDomainEventHandler<StudentAdmitted>, StudentAdmittedAuditHandler>();

            // Replaced by the Hangfire-backed signal in AddBackgroundJobs.
            services.AddSingleton<IOutboxSignal, NoOpOutboxSignal>();

            // Email goes through the outbox too.
            services.AddSingleton<IEmailQueue, OutboxEmailQueue>();
            services.AddSingleton<IOutboxMessageHandler, EmailOutboxHandler>();
            return services;
        }
    }



}
