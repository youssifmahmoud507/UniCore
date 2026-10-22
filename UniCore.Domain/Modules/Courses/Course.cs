using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Courses
{
    // The stable identity of a course (for example "CS101"). It belongs to a department, not to a program.
    // What can change (title, credits, hours, dates) lives in CourseVersion, so history is never rewritten.
    // Students, prerequisites and attempts are matched by Course, while offerings and grades point at a version.
    public sealed class Course : AggregateRoot
    {
        private Course()
        {
        }

        public Guid DepartmentId { get; private set; }

        // Upper-case, unique inside the department (database index on DepartmentId + Code), never changed.
        public string Code { get; private set; } = string.Empty;

        // The name shown in lists. The official title of a given period is CourseVersion.Title.
        public string Title { get; private set; } = string.Empty;

        public string? Description { get; private set; }
        public CourseType CourseType { get; private set; }
        public bool IsActive { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks that the department exists and is active.
        public static Result<Course> Create(Guid departmentId, string? code, string? title, string? description, CourseType? courseType, DateTimeOffset now)
        {
            if (departmentId == Guid.Empty)
            {
                return CoursesErrors.InvalidParent;
            }

            var cleanCode = code?.Trim().ToUpperInvariant();
            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, FieldRules.CodeMinLength, FieldRules.CodeMaxLength))
            {
                return CoursesErrors.InvalidCode;
            }

            var course = new Course { Id = Guid.NewGuid(), DepartmentId = departmentId, Code = cleanCode, IsActive = true, CreatedAt = now };

            var applied = course.Apply(title, description, courseType);
            if (applied.IsFailure)
            {
                return Result<Course>.Fail(applied.Errors);
            }

            return course;
        }

        public Result UpdateDetails(string? title, string? description, CourseType? courseType, DateTimeOffset now)
        {
            var applied = Apply(title, description, courseType);
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
                return Result.Fail(CoursesErrors.CourseAlreadyActive);
            }

            IsActive = true;
            UpdatedAt = now;
            return Result.Ok();
        }

        // The caller looks up whether the course still has an Active version (versions are separate aggregates).
        public Result Deactivate(bool hasActiveVersion, DateTimeOffset now)
        {
            if (!IsActive)
            {
                return Result.Fail(CoursesErrors.CourseAlreadyInactive);
            }

            if (hasActiveVersion)
            {
                return Result.Fail(CoursesErrors.CourseHasActiveVersion);
            }

            IsActive = false;
            UpdatedAt = now;
            return Result.Ok();
        }

        private Result Apply(string? title, string? description, CourseType? courseType)
        {
            var cleanTitle = title?.Trim();
            if (cleanTitle is null || cleanTitle.Length < FieldRules.NameMinLength || cleanTitle.Length > FieldRules.NameMaxLength)
            {
                return Result.Fail(CoursesErrors.InvalidTitle);
            }

            var cleanDescription = FieldRules.NormalizeOptional(description);
            if (cleanDescription is not null && cleanDescription.Length > FieldRules.DescriptionMaxLength)
            {
                return Result.Fail(CoursesErrors.InvalidDescription);
            }

            if (courseType is not { } type || !Enum.IsDefined(type))
            {
                return Result.Fail(CoursesErrors.InvalidCourseType);
            }

            Title = cleanTitle;
            Description = cleanDescription;
            CourseType = type;
            return Result.Ok();
        }
    }
}
