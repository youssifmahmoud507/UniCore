using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Admissions
{
    // One thing an applicant must provide for a program (for example "High school certificate").
    // The catalog entry is copied into each new application, so editing it later does not change
    // applications that already exist.
    public sealed class AdmissionRequirement : AggregateRoot
    {
        private AdmissionRequirement()
        {
        }

        public Guid AcademicProgramId { get; private set; }

        // Upper-case, unique inside the program (database index on AcademicProgramId + Code), never changed.
        public string Code { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public bool IsMandatory { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that the program exists before calling this.
        public static Result<AdmissionRequirement> Create(Guid academicProgramId, string? code, string? name, string? description, bool isMandatory, DateTimeOffset now)
        {
            if (academicProgramId == Guid.Empty)
            {
                return AdmissionsErrors.InvalidReference;
            }

            var cleanCode = code?.Trim().ToUpperInvariant();
            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return AdmissionsErrors.InvalidRequirementCode;
            }

            var requirement = new AdmissionRequirement
            {
                Id = Guid.NewGuid(),
                AcademicProgramId = academicProgramId,
                Code = cleanCode,
                IsActive = true,
                CreatedAt = now
            };

            var applied = requirement.Apply(name, description, isMandatory);

            if (applied.IsFailure)
            {
                return Result<AdmissionRequirement>.Fail(applied.Errors);
            }

            return requirement;
        }

        public Result UpdateDetails(string? name, string? description, bool isMandatory, DateTimeOffset now)
        {
            var applied = Apply(name, description, isMandatory);

            if (applied.IsFailure)
            {
                return applied;
            }

            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Activate(DateTimeOffset now)
        {
            if (IsActive)
            {
                return Result.Fail(AdmissionsErrors.AlreadyActive);
            }

            IsActive = true;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Deactivate(DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(AdmissionsErrors.AlreadyInactive);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result Apply(string? name, string? description, bool isMandatory)
        {
            var cleanName = name?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(AdmissionsErrors.InvalidRequirementName);
            }

            var cleanDescription = FieldRules.NormalizeOptional(description);
            if (cleanDescription is not null && cleanDescription.Length > FieldRules.DescriptionMaxLength)
            {
                return Result.Fail(AdmissionsErrors.InvalidRequirementDescription);
            }

            Name = cleanName;
            Description = cleanDescription;
            IsMandatory = isMandatory;
            return Result.Ok();
        }
    }
}
