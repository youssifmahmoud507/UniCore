using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Modules.Identity;

namespace UniCore.Infrastructure.Modules.Identity
{
    // Lives in Infrastructure: the Domain layer must not reference ASP.NET Core Identity.
    // This is a login account. It is not a Student or an Employee.
    public sealed class ApplicationUser : IdentityUser<Guid>
    {
        private ApplicationUser()
        {
        }
        public bool IsActive { get; private set; }
        public DateTimeOffset? LastLoginAt { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public static Result<ApplicationUser> Create(string? userName, string? email, string? phoneNumber, DateTimeOffset now)
        {
            var cleanUserName = userName?.Trim();
            if (string.IsNullOrEmpty(cleanUserName))
            {
                return IdentityErrors.UserNameRequired;
            }

            var cleanEmail = email?.Trim();
            if (string.IsNullOrEmpty(cleanEmail) || !MailAddress.TryCreate(cleanEmail, out var parsed) || !string.Equals(parsed.Address, cleanEmail, StringComparison.Ordinal))
            {
                return IdentityErrors.UserEmailInvalid;
            }

            var user = new ApplicationUser
            {
                // Id is left empty on purpose: EF Core generates the Guid when the user is added.
                UserName = cleanUserName,
                Email = cleanEmail,
                PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
                EmailConfirmed = false,
                IsActive = true,
                CreatedAt = now
            };

            return user;
        }

        public void Activate(DateTimeOffset now)
        {
            IsActive = true;
            UpdatedAt = now;
        }

        public void Deactivate(DateTimeOffset now)
        {
            IsActive = false;
            UpdatedAt = now;
        }

        public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;
    }
}
