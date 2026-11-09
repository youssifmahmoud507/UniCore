using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Examination;

namespace UniCore.Infrastructure.Modules.Examination
{
    // Everything the Examination module needs in the container. Called once from AddInfrastructure.
    public static class ExaminationServiceCollectionExtensions
    {
        public static IServiceCollection AddExaminationModule(this IServiceCollection services)
        {
            services.AddScoped<ExaminationAccess>();
            services.AddScoped<ExamConflictChecker>();
            services.AddScoped<ExamRosterService>();
            services.AddScoped<ExamSessionHandler>();
            services.AddScoped<ExamHandler>();

            return services;
        }
    }
}
