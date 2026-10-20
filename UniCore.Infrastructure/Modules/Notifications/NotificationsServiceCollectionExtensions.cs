using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Notifications;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Modules.Notifications
{
    public static class NotificationsServiceCollectionExtensions
    {
        public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
        {
            services.AddScoped<INotificationPublisher, NotificationPublisher>();
            services.AddScoped<IUserContactLookup, UserContactLookup>();
            services.AddScoped<IRoleHolderFinder, RoleHolderFinder>();
            services.AddScoped<INotificationSettings, NotificationSettings>();
            services.AddScoped<MyNotificationsHandler>();
            services.AddScoped<WorkflowNotifier>();
            services.AddScoped<StudentNotifier>();

            // Consumers of domain events (delivered by the outbox). Register this module AFTER AddPlatformModules,
            // so the financial-hold handlers of the same events run first.
            services.AddScoped<IDomainEventHandler<WorkflowStarted>, WorkflowStartedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowStepAdvanced>, WorkflowStepAdvancedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowCompleted>, WorkflowCompletedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowRejected>, WorkflowRejectedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowReturned>, WorkflowReturnedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowCancelled>, WorkflowCancelledNotificationHandler>();
            services.AddScoped<IDomainEventHandler<PaymentRecorded>, PaymentRecordedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<FinancialHoldPlaced>, FinancialHoldPlacedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<FinancialHoldReleased>, FinancialHoldReleasedNotificationHandler>();

            return services;
        }
    }

}
