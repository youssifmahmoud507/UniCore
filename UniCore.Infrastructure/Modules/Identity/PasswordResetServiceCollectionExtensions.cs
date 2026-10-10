using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniCore.Application.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public static class PasswordResetServiceCollectionExtensions
    {
        public static IServiceCollection AddPasswordReset(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IValidateOptions<PasswordResetOptions>, PasswordResetOptionsValidator>();

            services.AddOptions<PasswordResetOptions>()
                .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
                .ValidateOnStart();

            services.AddSingleton<IPasswordResetSettings>(sp => sp.GetRequiredService<IOptions<PasswordResetOptions>>().Value);

            // The reset token lives as long as the setting says (default 10 minutes).
            services.AddOptions<DataProtectionTokenProviderOptions>()
                .Configure<IOptions<PasswordResetOptions>>((tokenOptions, reset) =>
                    tokenOptions.TokenLifespan = TimeSpan.FromMinutes(reset.Value.ResetTokenMinutes));

            services.AddSingleton<IOtpService, OtpService>();
            services.AddScoped<IPasswordResetAccountService, PasswordResetAccountService>();

            services.AddScoped<ForgotPasswordHandler>();
            services.AddScoped<VerifyOtpHandler>();
            services.AddScoped<ResetPasswordHandler>();

            return services;
        }
    }




}
