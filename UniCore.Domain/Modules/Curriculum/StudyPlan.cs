using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Curriculum
{
    // The study plan of a program (for example "BSc CSE – 2026 plan"). It only holds the identity;
    // the content lives in its versions (StudyPlanVersion), which students are linked to.
    public sealed class StudyPlan : AggregateRoot
    {
        private StudyPlan()
        {
        }

        public Guid AcademicProgramId { get; private set; }

        // Upper-case, unique inside the program (database index on AcademicProgramId + Code), never changed.
        public string Code { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public string? Description { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that the program exists and is active.
        public static Result<StudyPlan> Create(Guid academicProgramId, string? code, string? name, string? description, DateTimeOffset now)
        {
            if (academicProgramId == Guid.Empty)
            {
                return CurriculumErrors.InvalidParent;
            }

            var cleanCode = code?.Trim().ToUpperInvariant();
            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return CurriculumErrors.InvalidCode;
            }

            var plan = new StudyPlan { Id = Guid.NewGuid(), AcademicProgramId = academicProgramId, Code = cleanCode, IsActive = true, CreatedAt = now };

            var applied = plan.Apply(name, description);
            if (applied.IsFailure)
            {
                return Result<StudyPlan>.Fail(applied.Errors);
            }

            return plan;
        }

        public Result UpdateDetails(string? name, string? description, DateTimeOffset now)
        {
            var applied = Apply(name, description);
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
                return Result.Fail(CurriculumErrors.PlanAlreadyActive);
            }

            IsActive = true;
            UpdatedAt = now;
            return Result.Ok();
        }

        // The caller looks up whether the plan still has an Active version (the versions are separate aggregates).
        public Result Deactivate(bool hasActiveVersion, DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(CurriculumErrors.PlanAlreadyInactive);
            }

            if (hasActiveVersion)
            {
                return Result.Fail(CurriculumErrors.PlanHasActiveVersion);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result Apply(string? name, string? description)
        {
            var cleanName = name?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(CurriculumErrors.InvalidName);
            }

            var cleanDescription = FieldRules.NormalizeOptional(description);
            if (cleanDescription is not null && cleanDescription.Length > FieldRules.DescriptionMaxLength)
            {
                return Result.Fail(CurriculumErrors.InvalidDescription);
            }

            Name = cleanName;
            Description = cleanDescription;
            return Result.Ok();
        }
    }
}
