using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Workflow;

namespace UniCore.Infrastructure.Modules.Maintenance
{
    public static class MaintenanceServiceCollectionExtensions
    {
        public static IServiceCollection AddMaintenanceModule(this IServiceCollection services)
        {
            services.AddOptions<MaintenanceOptions>().BindConfiguration(MaintenanceOptions.SectionName).Validate(MaintenanceOptions.IsValid, "Maintenance has an invalid value (every number of days must be 1 to 3650).").ValidateOnStart();

            services.AddScoped<ExpiredRequestsService>();
            services.AddSingleton<ExpiredRequestsRunner>();
            services.AddScoped<DataCleanupService>();

            services.AddSingleton<ExpiredRequestsJob>();
            services.AddSingleton<DataCleanupJob>();
            services.AddHostedService<MaintenanceJobRegistrar>();

            return services;
        }
    }

}
