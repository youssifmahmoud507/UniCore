using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Grading;

namespace UniCore.Infrastructure.Modules.Grading
{
    // Everything the Grading module needs in the container. Called once from AddInfrastructure.
    public static class GradingServiceCollectionExtensions
    {
        public static IServiceCollection AddGradingModule(this IServiceCollection services)
        {
            services.AddScoped<GradingAccess>();
            services.AddScoped<GradingEngine>();
            services.AddScoped<GradeEntryHandler>();
            services.AddScoped<GradeWorkflowHandler>();
            services.AddScoped<GradeQueryHandler>();
            services.AddScoped<IGradeReadService, GradeReadService>();
            services.AddScoped<IGradeCorrectionService, GradeCorrectionService>();
            services.AddScoped<GradeScaleSeeder>();

            return services;
        }

        // Called once at startup, after the identity seed.
        public static async Task<UniCore.Domain.Common.Results.Result> SeedGradingAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();

            return await scope.ServiceProvider.GetRequiredService<GradeScaleSeeder>().SeedAsync(cancellationToken);
        }
    }
}
