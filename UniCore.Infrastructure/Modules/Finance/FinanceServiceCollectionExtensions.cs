using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Finance;

namespace UniCore.Infrastructure.Modules.Finance
{
    public static class FinanceServiceCollectionExtensions
    {
        public static IServiceCollection AddFinance(this IServiceCollection services)
        {
            // The real verifier. UnverifiedFinanceReferenceVerifier stays in the project for the integration tests.
            services.AddScoped<IFinanceReferenceVerifier, FinanceReferenceVerifier>();
            services.AddScoped<IFinancialEligibilityService, PlaceholderFinancialEligibilityService>();

            services.AddScoped<FeeCatalogHandler>();
            services.AddScoped<FeeStructureHandler>();
            services.AddScoped<PaymentHandler>();

            services.AddScoped<DiscountHandler>();
            services.AddScoped<ScholarshipHandler>();
            services.AddScoped<StudentScholarshipHandler>();
            services.AddScoped<RefundHandler>();

            return services;
        }
    }




}
