using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Attendance;

namespace UniCore.Infrastructure.Modules.Attendance
{
    // Everything the Attendance module needs in the container. Called once from AddInfrastructure.
    public static class AttendanceServiceCollectionExtensions
    {
        public static IServiceCollection AddAttendanceModule(this IServiceCollection services)
        {
            services.AddScoped<AttendanceEvaluator>();
            services.AddScoped<IAttendanceReadService>(sp => sp.GetRequiredService<AttendanceEvaluator>());
            services.AddScoped<IAttendanceWarningService>(sp => sp.GetRequiredService<AttendanceEvaluator>());

            services.AddScoped<AttendanceAccess>();
            services.AddScoped<AttendancePolicyHandler>();
            services.AddScoped<AttendanceSessionHandler>();
            services.AddScoped<AttendanceExcuseHandler>();
            services.AddScoped<AttendanceReportHandler>();

            return services;
        }
    }
}
