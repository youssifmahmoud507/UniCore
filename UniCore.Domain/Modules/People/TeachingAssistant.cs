using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.People
{
    // A teaching assistant is an employee, a graduate student, or both: at least one of EmployeeId / StudentId.
    public sealed class TeachingAssistant : AggregateRoot
    {
        private TeachingAssistant()
        {
        }

        public Guid? EmployeeId { get; private set; }
        public Guid? StudentId { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that the employee / student exist (and the employee is active).
        public static Result<TeachingAssistant> Create(Guid? employeeId, Guid? studentId, DateTimeOffset now)
        {
            var hasEmployee = employeeId is { } e && e != Guid.Empty;
            var hasStudent = studentId is { } s && s != Guid.Empty;

            if (!hasEmployee && !hasStudent)
            {
                return PeopleErrors.TaRequiresEmployeeOrStudent;
            }

            return new TeachingAssistant
            {
                Id = Guid.NewGuid(),
                EmployeeId = hasEmployee ? employeeId : null,
                StudentId = hasStudent ? studentId : null,
                IsActive = true,
                CreatedAt = now
            };
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
    }
}
