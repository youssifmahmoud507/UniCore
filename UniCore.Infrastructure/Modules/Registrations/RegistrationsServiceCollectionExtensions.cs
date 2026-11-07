using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniCore.Application.Modules.Registrations;
using UniCore.Infrastructure.Modules.Registrations.Defaults;

namespace UniCore.Infrastructure.Modules.Registrations
{
    // Everything the Registrations module needs in the container. Called once from AddInfrastructure.
    public static class RegistrationsServiceCollectionExtensions
    {
        public static IServiceCollection AddRegistrationsModule(this IServiceCollection services)
        {
            services.AddScoped<IRegistrationSettings, ConfigRegistrationSettings>();

            // Defaults: TryAdd, so a real implementation registered BEFORE this call (or replaced after it) wins.
            services.TryAddScoped<IPrerequisiteEvaluator, NoPrerequisitesEvaluator>();
            services.TryAddScoped<IStudentCourseHistory, NoCourseHistory>();
            services.TryAddScoped<IScheduleConflictService, NoScheduleConflictService>();

            services.AddScoped<IEnrollmentReadService, EnrollmentReadService>();

            services.AddScoped<RegistrationAccess>();
            services.AddScoped<RegistrationPipeline>();
            services.AddScoped<RegistrationHandler>();
            services.AddScoped<RegistrationCompletionService>();
            services.AddScoped<RegistrationCompletionJob>();

            return services;
        }
    }
}
