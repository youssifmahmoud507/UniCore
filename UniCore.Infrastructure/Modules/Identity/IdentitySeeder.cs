using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UniCore.Application.Common.Abstractions;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class IdentitySeeder(RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager, IConfiguration configuration, IClock clock, ILogger<IdentitySeeder> logger)
    {
        private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly IConfiguration _configuration = configuration;
        private readonly IClock _clock = clock;
        private readonly ILogger<IdentitySeeder> _logger = logger;

        // Idempotent: roles and the admin that already exist are skipped.
        public async Task<Result> SeedAsync()
        {
            try
            {
                var roles = await SeedRolesAsync();
                if (roles.IsFailure)
                {
                    return roles;
                }

                return await SeedAdminAsync();
            }
            catch (Exception ex)
            {
                // Boundary: the database may be unreachable or not migrated yet. Log and return a Result.
                _logger.LogError("Identity seeding could not run ({ExceptionType}). Is the database migrated?", ex.GetType().Name);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }

        private async Task<Result> SeedRolesAsync()
        {
            var created = 0;

            foreach (var name in RoleNames.All)
            {
                if (await _roleManager.RoleExistsAsync(name))
                {
                    continue;
                }

                var role = ApplicationRole.Create(name);
                if (!role.TryGetValue(out var newRole))
                {
                    return Result.Fail(role.Errors);
                }

                var result = (await _roleManager.CreateAsync(newRole)).ToResult();
                if (result.IsFailure)
                {
                    _logger.LogError("Identity seeding failed for a role: {ErrorCode}", result.Error?.Code);
                    return result;
                }

                created++;
            }

            _logger.LogInformation("Identity seeding finished. Created {Count} role(s).", created);
            return Result.Ok();
        }

        // Creates the first SuperAdmin only when SeedAdmin:Email and SeedAdmin:Password are configured
        // (user-secrets or environment variables). Never configure it in production.
        private async Task<Result> SeedAdminAsync()
        {
            var email = _configuration["SeedAdmin:Email"];
            var password = _configuration["SeedAdmin:Password"];
            var userName = _configuration["SeedAdmin:UserName"] ?? "admin";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                return Result.Ok();
            }

            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                return Result.Ok();
            }

            var created = ApplicationUser.Create(userName, email, null, _clock.UtcNow);
            if (!created.TryGetValue(out var user))
            {
                return Result.Fail(created.Errors);
            }

            var createResult = (await _userManager.CreateAsync(user, password)).ToResult();
            if (createResult.IsFailure)
            {
                _logger.LogError("Seeding the admin user failed: {ErrorCode}", createResult.Error?.Code);
                return createResult;
            }

            var roleResult = (await _userManager.AddToRoleAsync(user, RoleNames.SuperAdmin)).ToResult();
            if (roleResult.IsFailure)
            {
                _logger.LogError("Assigning the SuperAdmin role failed: {ErrorCode}", roleResult.Error?.Code);
                return roleResult;
            }

            _logger.LogInformation("Seeded the initial SuperAdmin user.");
            return Result.Ok();
        }
    }
}
