using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Graduation;

namespace UniCore.Infrastructure.Modules.Graduation
{
    // Everything the Graduation module needs in the container. Called once from AddInfrastructure.
    public static class GraduationServiceCollectionExtensions
    {
        public static IServiceCollection AddGraduationModule(this IServiceCollection services)
        {
            services.AddScoped<GraduationAccess>();
            services.AddScoped<IGraduationEligibilityService, GraduationEligibilityService>();
            services.AddScoped<GraduationApplicationHandler>();
            services.AddScoped<GraduationDecisionHandler>();

            return services;
        }
    }
}
