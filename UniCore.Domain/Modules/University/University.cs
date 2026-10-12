using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.University
{
    // The institution itself. Root of its own aggregate: campuses and faculties reference it by UniversityId only.
    public sealed class University : AggregateRoot
    {
        private University()
        {
        }

        // Unique (database index). Stored upper-case and never changed after creation.
        public string Code { get; private set; } = string.Empty;

        // Unique (database index).
        public string Name { get; private set; } = string.Empty;

        public string? Description { get; private set; }
        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public string? Website { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        public static Result<University> Create(string? code, string? name, string? description, string? email, string? phone, string? website, DateTimeOffset now)
        {
            var cleanCode = code?.Trim().ToUpperInvariant();

            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return UniversityErrors.InvalidCode;
            }

            var university = new University
            {
                Id = Guid.NewGuid(),
                Code = cleanCode,
                IsActive = true,
                CreatedAt = now
            };

            // The new object is thrown away if the details are invalid, so nothing half-built escapes.
            var details = university.ApplyDetails(name, description, email, phone, website);

            if (details.IsFailure)
            {
                return Result<University>.Fail(details.Errors);
            }

            return university;
        }

        public Result UpdateDetails(string? name, string? description, string? email, string? phone, string? website, DateTimeOffset now)
        {
            var details = ApplyDetails(name, description, email, phone, website);

            if (details.IsFailure)
            {
                return details;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Activate(DateTimeOffset now)
        {
            if (IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyActive);
            }

            IsActive = true;
            UpdatedAt = now;
            return Result.Ok();
        }

        // The caller (application layer) looks up whether the university still has active faculties or campuses,
        // because this aggregate does not hold them.
        public Result Deactivate(bool hasActiveChildren, DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyInactive);
            }

            if (hasActiveChildren)
            {
                return Result.Fail(UniversityErrors.UniversityHasActiveChildren);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Validates everything first and assigns only if all of it is valid, so a failure never changes the entity.
        private Result ApplyDetails(string? name, string? description, string? email, string? phone, string? website)
        {
            var cleanName = name?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(UniversityErrors.InvalidName);
            }

            var cleanDescription = FieldRules.NormalizeOptional(description);
            if (cleanDescription is not null && cleanDescription.Length > FieldRules.DescriptionMaxLength)
            {
                return Result.Fail(UniversityErrors.InvalidDescription);
            }

            var cleanEmail = FieldRules.NormalizeOptional(email);
            if (cleanEmail is not null && !FieldRules.IsValidEmail(cleanEmail))
            {
                return Result.Fail(UniversityErrors.InvalidEmail);
            }

            var cleanPhone = FieldRules.NormalizeOptional(phone);
            if (cleanPhone is not null && !FieldRules.IsValidPhone(cleanPhone))
            {
                return Result.Fail(UniversityErrors.InvalidPhone);
            }

            var cleanWebsite = FieldRules.NormalizeOptional(website);
            if (cleanWebsite is not null && !FieldRules.IsValidWebsite(cleanWebsite))
            {
                return Result.Fail(UniversityErrors.InvalidWebsite);
            }

            Name = cleanName;
            Description = cleanDescription;
            Email = cleanEmail;
            Phone = cleanPhone;
            Website = cleanWebsite;
            return Result.Ok();
        }
    }
}
