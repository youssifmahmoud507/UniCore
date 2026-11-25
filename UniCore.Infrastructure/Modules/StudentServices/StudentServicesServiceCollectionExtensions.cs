using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.StudentServices;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Modules.StudentServices
{
    // Everything the Student Services module needs in the container. Called once from AddInfrastructure.
    public static class StudentServicesServiceCollectionExtensions
    {
        public static IServiceCollection AddStudentServicesModule(this IServiceCollection services)
        {
            services.AddScoped<ServiceRequestAccess>();
            services.AddScoped<CertificateRequestHandler>();
            services.AddScoped<TranscriptRequestHandler>();
            services.AddScoped<ComplaintHandler>();
            services.AddScoped<AppealHandler>();
            services.AddScoped<GradeAppealHandler>();

            // The request modules mirror the workflow engine's outcome.
            services.AddScoped<ServiceRequestWorkflowReactions>();
            services.AddScoped<IDomainEventHandler<WorkflowCompleted>, ServiceRequestWorkflowCompletedHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowRejected>, ServiceRequestWorkflowRejectedHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowReturned>, ServiceRequestWorkflowReturnedHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowCancelled>, ServiceRequestWorkflowCancelledHandler>();

            return services;
        }
    }
}
