using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.University
{
    // A degree program of a department (for example "BSc Computer Engineering").
    // Students, study plans and academic levels will reference it by AcademicProgramId.
    public sealed class AcademicProgram : AggregateRoot
    {
        public const int MaxDurationYears = 10;
        public const int MaxRequiredCredits = 400;

        private AcademicProgram()
        {
        }

        public Guid DepartmentId { get; private set; }

        // Unique inside the department (database index on DepartmentId + Code). Upper-case, never changed.
        public string Code { get; private set; } = string.Empty;

        public string Name { get; private set; } = string.Empty;
        public DegreeType DegreeType { get; private set; }
        public int DurationYears { get; private set; }
        public int TotalRequiredCredits { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer must check that the department exists and is active before calling this.
        public static Result<AcademicProgram> Create(
            Guid departmentId, string? code, string? name, DegreeType degreeType, int durationYears, int totalRequiredCredits, DateTimeOffset now)
        {
            if (departmentId == Guid.Empty)
            {
                return UniversityErrors.InvalidParent;
            }

            var cleanCode = code?.Trim().ToUpperInvariant();

            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return UniversityErrors.InvalidCode;
            }

            var program = new AcademicProgram
            {
                Id = Guid.NewGuid(),
                DepartmentId = departmentId,
                Code = cleanCode,
                IsActive = true,
                CreatedAt = now
            };

            var details = program.ApplyDetails(name, degreeType, durationYears, totalRequiredCredits);

            if (details.IsFailure)
            {
                return Result<AcademicProgram>.Fail(details.Errors);
            }

            return program;
        }

        public Result UpdateDetails(string? name, DegreeType degreeType, int durationYears, int totalRequiredCredits, DateTimeOffset now)
        {
            var details = ApplyDetails(name, degreeType, durationYears, totalRequiredCredits);

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

        // The caller looks up whether any student is still studying in this program (Students module).
        public Result Deactivate(bool hasActiveStudents, DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(UniversityErrors.AlreadyInactive);
            }

            if (hasActiveStudents)
            {
                return Result.Fail(UniversityErrors.ProgramHasActiveStudents);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result ApplyDetails(string? name, DegreeType degreeType, int durationYears, int totalRequiredCredits)
        {
            var cleanName = name?.Trim();
            if (cleanName is null || cleanName.Length < FieldRules.NameMinLength || cleanName.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(UniversityErrors.InvalidName);
            }

            if (!Enum.IsDefined(degreeType))
            {
                return Result.Fail(UniversityErrors.InvalidDegreeType);
            }

            if (durationYears < 1 || durationYears > MaxDurationYears)
            {
                return Result.Fail(UniversityErrors.InvalidDuration);
            }

            if (totalRequiredCredits < 1 || totalRequiredCredits > MaxRequiredCredits)
            {
                return Result.Fail(UniversityErrors.InvalidRequiredCredits);
            }

            Name = cleanName;
            DegreeType = degreeType;
            DurationYears = durationYears;
            TotalRequiredCredits = totalRequiredCredits;
            return Result.Ok();
        }
    }
}
