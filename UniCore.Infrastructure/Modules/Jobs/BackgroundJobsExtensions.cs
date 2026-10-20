using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UniCore.Infrastructure.Modules.Outbox;

namespace UniCore.Infrastructure.Modules.Jobs
{
    public static class BackgroundJobsExtensions
    {
        public const string DashboardPath = "/hangfire";
        public const string DashboardEnabledKey = "Hangfire:DashboardEnabled";

        // Hangfire with SQL Server storage (its own "hangfire" schema, created on first start)
        // and a worker server inside the API process. Call it after AddOutbox (it replaces the signal).
        public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, string connectionString)
        {
            services.AddHangfire(configuration => configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_180).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings().UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    SchemaName = "hangfire",
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));

            services.AddHangfireServer();

            services.AddSingleton<OutboxProcessingJob>();
            services.AddSingleton<FinancialHoldEvaluationJob>();
            services.Replace(ServiceDescriptor.Singleton<IOutboxSignal, HangfireOutboxSignal>());

            return services;
        }

        // Registers the recurring jobs (idempotent: running it on every start just updates them).
        public static IServiceProvider UseBackgroundJobs(this IServiceProvider services)
        {
            var recurring = services.GetRequiredService<IRecurringJobManager>();

            recurring.AddOrUpdate<OutboxProcessingJob>(OutboxProcessingJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), Cron.Minutely());

            recurring.AddOrUpdate<FinancialHoldEvaluationJob>(FinancialHoldEvaluationJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), Cron.Daily(2));

            recurring.AddOrUpdate<AdmissionExpiryJob>(AdmissionExpiryJob.RecurringJobId, job => job.RunAsync(CancellationToken.None), Cron.Daily(3));

            return services;
        }

        // The dashboard is OFF unless Hangfire:DashboardEnabled is true. When on, it is read-only
        // and answers only requests from the same machine.
        public static WebApplication MapBackgroundJobsDashboard(this WebApplication app)
        {
            if (!app.Configuration.GetValue<bool>(DashboardEnabledKey))
            {
                return app;
            }

            app.MapHangfireDashboard(DashboardPath, new DashboardOptions
            {
                Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
                IsReadOnlyFunc = _ => true
            });

            return app;
        }
    }


}
