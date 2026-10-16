using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Events;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.People
{
    // Raised when an employee is terminated. The scheduling module reacts by ending the employee's
    // open instructor assignments from TerminationDate (Week 8).
    public sealed record EmployeeTerminated(Guid EmployeeId, DateOnly TerminationDate, Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;

    // A person who works for the university. EmployeeNumber is unique. DepartmentId is optional (administrative staff).
    public sealed class Employee : AggregateRoot
    {
        public const int MaxJobTitleLength = 100;

        private Employee()
        {
        }

        public Guid PersonProfileId { get; private set; }
        public string EmployeeNumber { get; private set; } = string.Empty;
        public Guid? DepartmentId { get; private set; }
        public string? JobTitle { get; private set; }
        public DateOnly HireDate { get; private set; }
        public DateOnly? TerminationDate { get; private set; }
        public EmployeeStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that the person exists, the department (when given) exists and is active,
        // and that the person is not already an employee and the number is not used.
        public static Result<Employee> Create(Guid personProfileId, string? employeeNumber, Guid? departmentId, string? jobTitle, DateOnly hireDate, DateTimeOffset now)
        {
            if (personProfileId == Guid.Empty)
            {
                return PeopleErrors.InvalidParent;
            }

            var number = employeeNumber?.Trim().ToUpperInvariant();
            if (number is null || !FieldRules.IsValidCode(number, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return PeopleErrors.InvalidEmployeeNumber;
            }

            if (departmentId == Guid.Empty)
            {
                return PeopleErrors.InvalidParent;
            }

            var title = FieldRules.NormalizeOptional(jobTitle);
            if (title is not null && title.Length > MaxJobTitleLength)
            {
                return PeopleErrors.InvalidJobTitle;
            }

            return new Employee
            {
                Id = Guid.NewGuid(),
                PersonProfileId = personProfileId,
                EmployeeNumber = number,
                DepartmentId = departmentId,
                JobTitle = title,
                HireDate = hireDate,
                Status = EmployeeStatus.Active,
                CreatedAt = now
            };
        }

        public Result Update(Guid? departmentId, string? jobTitle, DateTimeOffset now)
        {
            if (Status == EmployeeStatus.Terminated)
            {
                return Result.Fail(PeopleErrors.EmployeeNotActive);
            }

            if (departmentId == Guid.Empty)
            {
                return Result.Fail(PeopleErrors.InvalidParent);
            }

            var title = FieldRules.NormalizeOptional(jobTitle);
            if (title is not null && title.Length > MaxJobTitleLength)
            {
                return Result.Fail(PeopleErrors.InvalidJobTitle);
            }

            DepartmentId = departmentId;
            JobTitle = title;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Terminate(DateOnly terminationDate, DateTimeOffset now)
        {
            if (Status == EmployeeStatus.Terminated)
            {
                return Result.Fail(PeopleErrors.EmployeeAlreadyTerminated);
            }

            if (terminationDate < HireDate)
            {
                return Result.Fail(PeopleErrors.InvalidTerminationDate);
            }

            Status = EmployeeStatus.Terminated;
            TerminationDate = terminationDate;
            UpdatedAt = now;
            RaiseDomainEvent(new EmployeeTerminated(Id, terminationDate, Guid.NewGuid(), now));
            return Result.Ok();
        }
    }
}
