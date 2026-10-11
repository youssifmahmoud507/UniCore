using System.Text.RegularExpressions;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Identity
{
    public sealed class Permission
    {
        private static readonly Regex CodePattern = new("^[A-Z][A-Za-z]*\\.[A-Z][A-Za-z]*$", RegexOptions.Compiled);

        private Permission()
        {
        }

        public Guid Id { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public string Module { get; private set; } = string.Empty;

        public static Result<Permission> Create(string? code, string? name, string? description, string? module)
        {
            var cleanCode = code?.Trim();
            if (string.IsNullOrEmpty(cleanCode) || cleanCode.Length > 100 || !CodePattern.IsMatch(cleanCode))
            {
                return IdentityErrors.PermissionCodeInvalid;
            }

            var cleanName = name?.Trim();
            var cleanModule = module?.Trim();
            var cleanDescription = description?.Trim();

            if (string.IsNullOrEmpty(cleanName) || cleanName.Length > 200 || string.IsNullOrEmpty(cleanModule) || cleanModule.Length > 50 || (cleanDescription is not null && cleanDescription.Length > 500))
            {
                return IdentityErrors.PermissionDataInvalid;
            }

            return new Permission
            {
                Id = Guid.NewGuid(),
                Code = cleanCode,
                Name = cleanName,
                Description = string.IsNullOrEmpty(cleanDescription) ? null : cleanDescription,
                Module = cleanModule
            };
        }
    }

}
