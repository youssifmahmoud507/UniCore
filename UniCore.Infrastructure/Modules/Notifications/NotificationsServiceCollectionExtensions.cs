using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Notifications;
using UniCore.Domain.Modules.Assessments;
using UniCore.Domain.Modules.Attendance;
using UniCore.Domain.Modules.Examination;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.Grading;
using UniCore.Domain.Modules.Registrations;
using UniCore.Domain.Modules.Transcripts;
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
            services.AddScoped<AcademicNotifier>();

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

            // Events of the academic modules: the students concerned are told.
            services.AddScoped<IDomainEventHandler<AttendanceWarningIssued>, AttendanceWarningNotificationHandler>();
            services.AddScoped<IDomainEventHandler<GradePublished>, GradePublishedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<GradeCorrected>, GradeCorrectedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<AssessmentPublished>, AssessmentPublishedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<AssessmentCancelled>, AssessmentCancelledNotificationHandler>();
            services.AddScoped<IDomainEventHandler<ExamPublished>, ExamPublishedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<ExamCancelled>, ExamCancelledNotificationHandler>();
            services.AddScoped<IDomainEventHandler<EnrollmentCreated>, EnrollmentCreatedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<EnrollmentDropped>, EnrollmentDroppedNotificationHandler>();
            services.AddScoped<IDomainEventHandler<TranscriptIssued>, TranscriptIssuedNotificationHandler>();

            return services;
        }
    }

}
