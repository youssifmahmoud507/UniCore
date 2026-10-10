using Microsoft.AspNetCore.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    // Values from identity.md (proposed defaults). Moves to the Options pattern later.
    public static class IdentityOptionsSetup
    {
        public static void Configure(IdentityOptions options)
        {
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
        }
    }
}
