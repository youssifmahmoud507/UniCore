using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using UniCore.Application.Modules.Audit;
using UniCore.Application.Modules.Finance;
using UniCore.Infrastructure.Modules.Audit;
using UniCore.Infrastructure.Modules.Finance;
using UniCore.Infrastructure.Modules.Jobs;
using UniCore.Infrastructure.Modules.Outbox;

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

            return services;
        }
    }
}
