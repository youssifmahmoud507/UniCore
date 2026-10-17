using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using UniCore.Application.Common.Abstractions;
using UniCore.Application.Modules.Finance;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Finance;
using UniCore.Domain.Modules.University;
using UniCore.Infrastructure.Persistence;

namespace UniCore.Infrastructure.Modules.Finance
{
    public static class FinanceServiceCollectionExtensions
    {
        public static IServiceCollection AddFinance(this IServiceCollection services)
        {
            // The real verifier. UnverifiedFinanceReferenceVerifier stays in the project for the integration tests.
            services.AddScoped<IFinanceReferenceVerifier, FinanceReferenceVerifier>();

            services.AddScoped<FeeCatalogHandler>();
            services.AddScoped<FeeStructureHandler>();
            services.AddScoped<PaymentHandler>();

            services.AddScoped<DiscountHandler>();
            services.AddScoped<ScholarshipHandler>();
            services.AddScoped<StudentScholarshipHandler>();
            services.AddScoped<RefundHandler>();

            // Financial holds and the eligibility contract used by Developer 1.
            services.AddOptions<FinancialHoldOptions>().BindConfiguration(FinancialHoldOptions.SectionName).Validate(FinancialHoldOptions.IsValid, "Finance:Holds has an invalid value.").ValidateOnStart();

            services.AddSingleton<IFinancialHoldPolicy, FinancialHoldPolicy>();
            services.AddSingleton<IFinancialHoldRunner, FinancialHoldRunner>();

            services.AddScoped<IFinancialEligibilityService, FinancialEligibilityService>();
            services.AddScoped<FinancialHoldEvaluator>();
            services.AddScoped<FinancialHoldHandler>();
            services.AddScoped<StudentFinanceHandler>();

            services.AddScoped<IDomainEventHandler<PaymentRecorded>, PaymentRecordedHoldHandler>();
            services.AddScoped<IDomainEventHandler<RefundProcessed>, RefundProcessedHoldHandler>();

            // F6: service charges, the invoice issuer, bulk invoicing and the student's own finance.
            services.AddScoped<InvoiceIssuer>();
            services.AddScoped<IStudentChargeService, StudentChargeService>();
            services.AddScoped<ServiceChargeHandler>();
            services.AddScoped<BulkInvoiceHandler>();
            services.AddScoped<MyFinanceHandler>();
            services.AddSingleton<IInvoiceBatchRunner, InvoiceBatchRunner>();

            return services;
        }
    }

}
