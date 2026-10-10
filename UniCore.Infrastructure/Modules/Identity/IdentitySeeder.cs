using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class IdentitySeeder
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ILogger<IdentitySeeder> _logger;

        public IdentitySeeder(RoleManager<ApplicationRole> roleManager, ILogger<IdentitySeeder> logger)
        {
            _roleManager = roleManager;
            _logger = logger;
        }

        // Idempotent: roles that already exist are skipped.
        public async Task<Result> SeedAsync()
        {
            try
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
            catch (Exception ex)
            {
                // Boundary: the database may be unreachable or not migrated yet. Log and return a Result.
                _logger.LogError("Identity seeding could not run ({ExceptionType}). Is the database migrated?", ex.GetType().Name);
                return Result.Fail(CommonErrors.ExternalServiceUnavailable);
            }
        }
    }
}
