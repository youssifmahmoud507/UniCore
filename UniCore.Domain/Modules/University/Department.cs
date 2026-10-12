using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.University
{
    // A department of a faculty. Programs and courses will reference it by DepartmentId.
    // The data model has no timestamps for departments, so there is no `now` here.
    public sealed class Department : AggregateRoot
    {
        private Department()
        {
        }

        public Guid FacultyId { get; private set; }

        // Unique inside the faculty (database index on FacultyId + Code). Upper-case, never changed.
        public string Code { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public string? Email { get; private set; }
        public string? Phone { get; private set; }
        public bool IsActive { get; private set; }

        // The application layer must check that the faculty exists and is active before calling this.
        public static Result<Department> Create(
            Guid facultyId, string? code, string? name, string? description, string? email, string? phone)
        {
            if (facultyId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            var cleanCode = code?.Trim().ToUpperInvariant();

            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return UniversityErrors.InvalidCode;
            }

            var department = new Department
            {
                Id = Guid.NewGuid(),
                FacultyId = facultyId,
                Code = cleanCode,
                IsActive = true
            };

            var details = department.ApplyDetails(name, description, email, phone);

            if (details.IsFailure)
            {
                return Result<Department>.Fail(details.Errors);
            }

            return department;
        }

        public Result UpdateDetails(string? name, string? description, string? email, string? phone)
        {
            return ApplyDetails(name, description, email, phone);
        }

        public Result Activate()
        {
            if (IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyActive);
            }

            IsActive = true;
            return Result.Ok();
        }

        // The caller looks up whether the department still has active academic programs (Week 3).
        public Result Deactivate(bool hasActivePrograms)
        {
            if (!IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyInactive);
            }

            if (hasActivePrograms)
            {
                return Result.Fail(UniversityErrors.DepartmentHasActivePrograms);
            }

            IsActive = false;
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
