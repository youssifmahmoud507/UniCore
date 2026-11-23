using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Grading;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Workflow;

namespace UniCore.Infrastructure.Modules.Workflow
{
    public static class WorkflowServiceCollectionExtensions
    {
        public static IServiceCollection AddWorkflowModule(this IServiceCollection services)
        {
            services.AddScoped<WorkflowApproverCheck>();
            services.AddScoped<IWorkflowService, WorkflowService>();
            services.AddScoped<WorkflowDefinitionHandler>();
            services.AddScoped<WorkflowQueryHandler>();
            services.AddScoped<WorkflowDefinitionSeeder>();

            // The grade change request (Developer 2) is approved by a workflow and applies the correction when it completes.
            services.AddScoped<GradeChangeHandler>();
            services.AddScoped<GradeChangeWorkflowHandlers>();
            services.AddScoped<IDomainEventHandler<WorkflowCompleted>, GradeChangeWorkflowCompletedHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowRejected>, GradeChangeWorkflowRejectedHandler>();
            services.AddScoped<IDomainEventHandler<WorkflowCancelled>, GradeChangeWorkflowCancelledHandler>();

            return services;
        }

        // Called once at startup, after the identity seed.
        public static async Task<Result> SeedWorkflowAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();

            return await scope.ServiceProvider.GetRequiredService<WorkflowDefinitionSeeder>().SeedAsync(cancellationToken);
        }
    }
}
