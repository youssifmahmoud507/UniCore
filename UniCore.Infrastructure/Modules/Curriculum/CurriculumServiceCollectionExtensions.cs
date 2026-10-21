using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Curriculum;

namespace UniCore.Infrastructure.Modules.Curriculum
{
    // Everything the Curriculum module needs in the container. Called once from AddInfrastructure.
    public static class CurriculumServiceCollectionExtensions
    {
        public static IServiceCollection AddCurriculumModule(this IServiceCollection services)
        {
            services.AddScoped<ICourseVersionVerifier, UnverifiedCourseVersionVerifier>();
            services.AddScoped<IStudyPlanReadService, StudyPlanReadService>();

            services.AddScoped<StudyPlanHandler>();
            services.AddScoped<StudyPlanVersionHandler>();

            return services;
        }
    }
}
