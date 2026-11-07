using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Registrations;
using UniCore.Application.Modules.Scheduling;

namespace UniCore.Infrastructure.Modules.Scheduling
{
    // Everything the Scheduling module needs in the container. Called once from AddInfrastructure, BEFORE
    // AddRegistrationsModule: Registrations only registers its "never a conflict" default when nobody else has.
    public static class SchedulingServiceCollectionExtensions
    {
        public static IServiceCollection AddSchedulingModule(this IServiceCollection services)
        {
            services.AddScoped<IScheduleConflictService, ScheduleConflictService>();
            services.AddScoped<IScheduleReadService, ScheduleReadService>();

            services.AddScoped<SchedulingAccess>();
            services.AddScoped<ScheduleHandler>();
            services.AddScoped<SectionScheduleHandler>();
            services.AddScoped<AvailabilityHandler>();

            return services;
        }
    }
}
