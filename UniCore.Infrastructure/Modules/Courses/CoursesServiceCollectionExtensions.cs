using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Courses;
using UniCore.Application.Modules.Curriculum;

namespace UniCore.Infrastructure.Modules.Courses
{
    // Everything the Courses module needs in the container. Called once from AddInfrastructure.
    public static class CoursesServiceCollectionExtensions
    {
        public static IServiceCollection AddCoursesModule(this IServiceCollection services)
        {
            // Curriculum's contract, answered by Courses.
            services.AddScoped<ICourseVersionVerifier, CourseVersionVerifier>();
            services.AddScoped<ICourseCatalogReadService, CourseCatalogReadService>();

            services.AddScoped<CourseHandler>();
            services.AddScoped<CourseVersionHandler>();

            return services;
        }
    }
}
