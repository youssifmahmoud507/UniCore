using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace UniCore.Infrastructure.Modules.Email
{
    public sealed class SmtpOptionsValidator : IValidateOptions<SmtpOptions>
    {
        public ValidateOptionsResult Validate(string? name, SmtpOptions options)
        {
            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Host))
            {
                failures.Add("Smtp:Host is required.");
            }

            if (options.Port is < 1 or > 65535)
            {
                failures.Add("Smtp:Port must be between 1 and 65535.");
            }

            if (!MailAddress.TryCreate(options.FromAddress, out _))
            {
                failures.Add("Smtp:FromAddress must be a valid email address.");
            }

            if (string.IsNullOrWhiteSpace(options.FromName))
            {
                failures.Add("Smtp:FromName is required.");
            }

            if (options.TimeoutSeconds <= 0)
            {
                failures.Add("Smtp:TimeoutSeconds must be greater than zero.");
            }

            if (!string.IsNullOrEmpty(options.Username) && string.IsNullOrEmpty(options.Password))
            {
                failures.Add("Smtp:Password is required when Smtp:Username is set.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }



}
