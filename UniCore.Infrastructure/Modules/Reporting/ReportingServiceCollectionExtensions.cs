using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Reporting;
using UniCore.Application.Modules.Reporting.Dashboard;
using UniCore.Application.Modules.Reporting.Reports;
using UniCore.Infrastructure.Modules.Reporting.Export;

namespace UniCore.Infrastructure.Modules.Reporting
{
    public static class ReportingServiceCollectionExtensions
    {
        // A new report from any module: write an IReportProvider and add one AddScoped<IReportProvider, ...> line here.
        public static IServiceCollection AddReportingModule(this IServiceCollection services)
        {
            services.AddScoped<IReportDataSource, ReportDataSource>();
            services.AddScoped<IReportScopeResolver, ReportScopeResolver>();

            services.AddScoped<IReportProvider, FinanceInvoiceSummaryReport>();
            services.AddScoped<IReportProvider, FinanceOutstandingBalancesReport>();
            services.AddScoped<IReportProvider, FinanceCollectionsReport>();
            services.AddScoped<IReportProvider, WorkflowRequestsReport>();
            services.AddScoped<IReportProvider, DocumentsByTypeReport>();
            services.AddScoped<IReportProvider, NotificationDeliveryReport>();
            services.AddScoped<IReportProvider, AuditActivityReport>();
            services.AddScoped<ReportCatalog>();

            services.AddSingleton<IReportExporter, CsvReportExporter>();
            services.AddSingleton<IReportExporter, XlsxReportExporter>();
            services.AddSingleton<IReportExporter, PdfReportExporter>();
            services.AddScoped<ReportHandler>();

            services.AddScoped<IDashboardWidget, FinanceDashboardWidget>();
            services.AddScoped<IDashboardWidget, RequestsDashboardWidget>();
            services.AddScoped<IDashboardWidget, DocumentsDashboardWidget>();
            services.AddScoped<IDashboardWidget, SecurityDashboardWidget>();
            services.AddScoped<IDashboardWidget, NotificationsDashboardWidget>();
            services.AddScoped<DashboardHandler>();

            return services;
        }
    }




}
