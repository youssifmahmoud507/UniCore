using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Workflow;
using UniCore.Domain.Common.Results;

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
