using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Modules.Audit;
using UniCore.Application.Modules.Finance;
using UniCore.Infrastructure.Modules.Audit;
using UniCore.Infrastructure.Modules.Caching;
using UniCore.Infrastructure.Modules.Documents;
using UniCore.Infrastructure.Modules.Finance;
using UniCore.Infrastructure.Modules.Idempotency;
using UniCore.Infrastructure.Modules.Jobs;
using UniCore.Infrastructure.Modules.Maintenance;
using UniCore.Infrastructure.Modules.Notifications;
using UniCore.Infrastructure.Modules.Outbox;
using UniCore.Infrastructure.Modules.Reporting;
using UniCore.Infrastructure.Modules.Workflow;

namespace UniCore.Infrastructure.Modules.Platform
{
    // Everything Developer 2 owns, in ONE call, so a merge of the shared DependencyInjection.cs cannot silently drop it.
    public static class PlatformServiceCollectionExtensions
    {
        public static IServiceCollection AddPlatformModules(this IServiceCollection services, string connectionString)
        {
            services.AddScoped<IAuditLogReader, AuditLogReader>();
            services.AddScoped<AuditLogQueryHandler>();

            // AddOutbox must come before AddBackgroundJobs (it replaces the outbox signal).
            services.AddOutbox();
            services.AddFinance();
            services.AddBackgroundJobs(connectionString);

            services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
            services.AddScoped<ChargeHandler>();
            services.AddScoped<InvoiceHandler>();
            services.AddWorkflowModule();
            services.AddNotificationsModule();
            // Notifications after the finance and workflow handlers, so holds are handled before people are told.
            services.AddRealtime();
            services.AddDocumentsModule();
            services.AddMaintenanceModule();
            services.AddIdempotencyModule();
            services.AddReportingModule();
            // Last: it puts the cached provider in front of the user access provider registered earlier.
            services.AddCachingModule();
            return services;
        }
    }
}
