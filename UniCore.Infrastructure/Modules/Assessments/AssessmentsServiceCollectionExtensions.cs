using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Assessments;

namespace UniCore.Infrastructure.Modules.Assessments
{
    // Everything the Assessments module needs in the container. Called once from AddInfrastructure.
    public static class AssessmentsServiceCollectionExtensions
    {
        public static IServiceCollection AddAssessmentsModule(this IServiceCollection services)
        {
            services.AddScoped<AssessmentAccess>();
            services.AddScoped<AssessmentHandler>();
            services.AddScoped<IAssessmentReadService, AssessmentReadService>();

            return services;
        }
    }
}
