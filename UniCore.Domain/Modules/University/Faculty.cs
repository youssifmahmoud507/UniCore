using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.University
{
    // A faculty of a university. Root of its own aggregate; departments reference it by FacultyId only.
    public sealed class Faculty : AggregateRoot
    {
        private Faculty()
        {
        }

        public Guid UniversityId { get; private set; }

        // Unique inside the university (database index on UniversityId + Code). Upper-case, never changed.
        public string Code { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer must check that the university exists and is active before calling this.
        public static Result<Faculty> Create(
            Guid universityId, string? code, string? name, string? description, string? email, string? phone, DateTimeOffset now)
        {
            if (universityId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            var cleanCode = code?.Trim().ToUpperInvariant();

            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return UniversityErrors.InvalidCode;
            }

            var faculty = new Faculty
            {
                Id = Guid.NewGuid(),
                UniversityId = universityId,
                Code = cleanCode,
                IsActive = true,
                CreatedAt = now
            };

            var details = faculty.ApplyDetails(name, description, email, phone);

            if (details.IsFailure)
            {
                return Result<Faculty>.Fail(details.Errors);
            }

            return faculty;
        }

        public Result UpdateDetails(string? name, string? description, string? email, string? phone, DateTimeOffset now)
        {
            var details = ApplyDetails(name, description, email, phone);

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

        // The caller looks up whether the faculty still has active departments.
        public Result Deactivate(bool hasActiveDepartments, DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyInactive);
            }

            if (hasActiveDepartments)
            {
                return Result.Fail(UniversityErrors.FacultyHasActiveDepartments);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result ApplyDetails(string? name, string? description, string? email, string? phone)
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

            Name = cleanName;
            Description = cleanDescription;
            Email = cleanEmail;
            Phone = cleanPhone;
            return Result.Ok();
        }
    }
}
