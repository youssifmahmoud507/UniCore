using Microsoft.Extensions.DependencyInjection;
using UniCore.Application.Modules.Idempotency;

namespace UniCore.Infrastructure.Modules.Idempotency
{
    public static class IdempotencyServiceCollectionExtensions
    {
        public static IServiceCollection AddIdempotencyModule(this IServiceCollection services)
        {
            services.AddOptions<IdempotencyOptions>().BindConfiguration(IdempotencyOptions.SectionName).Validate(IdempotencyOptions.IsValid, "Idempotency has an invalid value (ExpiryHours 1 to 168, InProgressTimeoutSeconds 10 to 3600).").ValidateOnStart();

            services.AddScoped<IIdempotencyService, IdempotencyService>();

            return services;
        }
    }
}
