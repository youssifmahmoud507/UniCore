using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UniCore.Application.Common.Abstractions;

namespace UniCore.Infrastructure.Modules.Email
{
    public static class EmailServiceCollectionExtensions
    {
        public static IServiceCollection AddEmailSending(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();

            services.AddOptions<SmtpOptions>()
                .Bind(configuration.GetSection(SmtpOptions.SectionName))
                .ValidateOnStart();

            services.AddSingleton<IEmailSender, SmtpEmailSender>();
            services.AddSingleton<EmailQueue>();
            services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
            services.AddHostedService<EmailDispatchService>();

            return services;
        }
    }



}
