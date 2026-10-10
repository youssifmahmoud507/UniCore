using Microsoft.AspNetCore.Identity;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    public sealed class ApplicationRole : IdentityRole<Guid>
    {
        private ApplicationRole()
        {
        }

        public static Result<ApplicationRole> Create(string? name)
        {
            var cleanName = name?.Trim();
            if (string.IsNullOrEmpty(cleanName))
            {
                return IdentityErrors.RoleNameRequired;
            }

            return new ApplicationRole { Name = cleanName };
        }
    }
}
