using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Admissions;
using UniCore.Infrastructure.Modules.Jobs;

namespace UniCore.Infrastructure.Modules.Admissions
{
    // Everything the Admissions module needs in the container. Called once from AddInfrastructure.
    public static class AdmissionsServiceCollectionExtensions
    {
        public static IServiceCollection AddAdmissionsModule(this IServiceCollection services)
        {
            services.AddScoped<IAdmissionDeadlineProvider, AdmissionDeadlineProvider>();
            services.AddScoped<IAdmissionDocumentVerifier, UnverifiedAdmissionDocumentVerifier>();

            services.AddScoped<ApplicantHandler>();
            services.AddScoped<AdmissionRequirementHandler>();
            services.AddScoped<AdmissionApplicationHandler>();
            services.AddScoped<AdmissionExpiryService>();

            // Hangfire creates a scope per job run, so the job can depend on the scoped service. It is registered
            // as a recurring job in UseBackgroundJobs.
            services.AddScoped<AdmissionExpiryJob>();

            return services;
        }
    }
}
