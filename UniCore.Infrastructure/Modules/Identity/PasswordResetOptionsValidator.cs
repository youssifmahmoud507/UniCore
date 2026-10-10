using Microsoft.Extensions.Options;
using System.Text;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class PasswordResetOptionsValidator : IValidateOptions<PasswordResetOptions>
    {
        private const int MinimumPepperBytes = 32;

        public ValidateOptionsResult Validate(string? name, PasswordResetOptions options)
        {
            var failures = new List<string>();

            // Never include the pepper itself in a message.
            if (Encoding.UTF8.GetByteCount(options.Pepper ?? string.Empty) < MinimumPepperBytes)
            {
                failures.Add($"PasswordReset:Pepper must be at least {MinimumPepperBytes} bytes long.");
            }

            if (string.IsNullOrWhiteSpace(options.AppName))
            {
                failures.Add("PasswordReset:AppName is required.");
            }

            if (options.OtpLifetimeMinutes <= 0)
            {
                failures.Add("PasswordReset:OtpLifetimeMinutes must be greater than zero.");
            }

            if (options.MaxAttempts <= 0)
            {
                failures.Add("PasswordReset:MaxAttempts must be greater than zero.");
            }

            if (options.ResendCooldownSeconds < 0)
            {
                failures.Add("PasswordReset:ResendCooldownSeconds cannot be negative.");
            }

            if (options.MaxRequestsPerHour <= 0)
            {
                failures.Add("PasswordReset:MaxRequestsPerHour must be greater than zero.");
            }

            if (options.ResetTokenMinutes <= 0)
            {
                failures.Add("PasswordReset:ResetTokenMinutes must be greater than zero.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }




}
