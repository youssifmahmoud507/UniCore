using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.People
{
    // An employee who teaches. It has no DepartmentId of its own: the department is the employee's.
    // One instructor per employee (database index on EmployeeId).
    public sealed class Instructor : AggregateRoot
    {
        public const int MaxSpecializationLength = 200;

        private Instructor()
        {
        }

        public Guid EmployeeId { get; private set; }
        public AcademicRank Rank { get; private set; }
        public string? Specialization { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that the employee exists and is active, and is not already an instructor.
        public static Result<Instructor> Create(Guid employeeId, AcademicRank rank, string? specialization, DateTimeOffset now)
        {
            if (employeeId == Guid.Empty)
            {
                return PeopleErrors.InvalidParent;
            }

            var instructor = new Instructor { Id = Guid.NewGuid(), EmployeeId = employeeId, IsActive = true, CreatedAt = now };

            var details = instructor.ApplyDetails(rank, specialization);
            if (details.IsFailure)
            {
                return Result<Instructor>.Fail(details.Errors);
            }

            return instructor;
        }

        public Result Update(AcademicRank rank, string? specialization, DateTimeOffset now)
        {
            var details = ApplyDetails(rank, specialization);
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
                return Result.Fail(PeopleErrors.AlreadyActive);
            }

            IsActive = true;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Deactivate(DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(PeopleErrors.AlreadyInactive);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result ApplyDetails(AcademicRank rank, string? specialization)
        {
            if (!Enum.IsDefined(rank))
            {
                return Result.Fail(PeopleErrors.InvalidAcademicRank);
            }

            var clean = FieldRules.NormalizeOptional(specialization);
            if (clean is not null && clean.Length > MaxSpecializationLength)
            {
                return Result.Fail(PeopleErrors.InvalidSpecialization);
            }

            Rank = rank;
            Specialization = clean;
            return Result.Ok();
        }
    }
}
