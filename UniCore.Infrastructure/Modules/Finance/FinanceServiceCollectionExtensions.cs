using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Finance;

namespace UniCore.Infrastructure.Modules.Finance
{
    public static class FinanceServiceCollectionExtensions
    {
        public static IServiceCollection AddFinance(this IServiceCollection services)
        {
            services.AddScoped<IFinanceReferenceVerifier, UnverifiedFinanceReferenceVerifier>();
            services.AddScoped<IFinancialEligibilityService, PlaceholderFinancialEligibilityService>();

            services.AddScoped<FeeCatalogHandler>();
            services.AddScoped<FeeStructureHandler>();

            return services;
        }
    }




}
