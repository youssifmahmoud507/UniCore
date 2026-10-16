using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Calendar;

namespace UniCore.Infrastructure.Modules.Calendar
{
    public static class CalendarServiceCollectionExtensions
    {
        public static IServiceCollection AddCalendarModule(this IServiceCollection services)
        {
            services.AddScoped<IAcademicClock, AcademicClock>();
            services.AddScoped<IAcademicCalendarService, AcademicCalendarService>();
            services.AddScoped<AcademicYearHandler>();
            services.AddScoped<AcademicTermHandler>();
            services.AddScoped<AcademicSemesterHandler>();
            services.AddScoped<AcademicLevelHandler>();
            return services;
        }
    }
}
