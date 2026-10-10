using Microsoft.Extensions.Options;
using System.Text;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        private const int MinimumKeyBytes = 32;

        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            var failures = new List<string>();

            if (string.IsNullOrWhiteSpace(options.Issuer))
            {
                failures.Add("Jwt:Issuer is required.");
            }

            if (string.IsNullOrWhiteSpace(options.Audience))
            {
                failures.Add("Jwt:Audience is required.");
            }

            // Never include the key itself in a message.
            if (Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) < MinimumKeyBytes)
            {
                failures.Add($"Jwt:SigningKey must be at least {MinimumKeyBytes} bytes long.");
            }

            if (options.AccessTokenMinutes <= 0)
            {
                failures.Add("Jwt:AccessTokenMinutes must be greater than zero.");
            }

            if (options.RefreshTokenDays <= 0)
            {
                failures.Add("Jwt:RefreshTokenDays must be greater than zero.");
            }

            if (options.ClockSkewSeconds < 0)
            {
                failures.Add("Jwt:ClockSkewSeconds cannot be negative.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }

}
