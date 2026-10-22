using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Courses
{
    // One version of a course: title, credits, hours and the dates it is effective. Offerings, enrollments and
    // grades point at a version and keep it, so an Active or Retired version never changes. To change credits,
    // hours or content, create a new draft (copying the old one), edit it and activate it: activating retires the
    // version it replaces on the day before (handler). Versions of one course never overlap in time.
    public sealed class CourseVersion : AggregateRoot
    {
        public const int MaxCredits = 30;
        public const int MaxHours = 60;

        private CourseVersion()
        {
        }

        public Guid CourseId { get; private set; }

        // 1, 2, 3 ... inside the course (database index on CourseId + VersionNumber).
        public int VersionNumber { get; private set; }

        public string Title { get; private set; } = string.Empty;
        public string? Description { get; private set; }

        // Credit hours. The registration credit limit counts these.
        public int Credits { get; private set; }

        public int LectureHours { get; private set; }
        public int PracticalHours { get; private set; }
        public DateOnly EffectiveFrom { get; private set; }
        public DateOnly? EffectiveTo { get; private set; }
        public CourseVersionStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer picks the next version number and checks that the course is active.
        public static Result<CourseVersion> Create(
            Guid courseId, int versionNumber, string? title, string? description, int? credits, int? lectureHours, int? practicalHours,
            DateOnly effectiveFrom, DateTimeOffset now)
        {
            if (courseId == Guid.Empty)
            {
                return CoursesErrors.InvalidParent;
            }

            if (versionNumber < 1)
            {
                return CoursesErrors.VersionNumberInvalid;
            }

            if (effectiveFrom == default)
            {
                return CoursesErrors.EffectiveDateInvalid;
            }

            var version = new CourseVersion
            {
                Id = Guid.NewGuid(),
                CourseId = courseId,
                VersionNumber = versionNumber,
                EffectiveFrom = effectiveFrom,
                Status = CourseVersionStatus.Draft,
                CreatedAt = now
            };

            var applied = version.Apply(title, description, credits, lectureHours, practicalHours);
            if (applied.IsFailure)
            {
                return Result<CourseVersion>.Fail(applied.Errors);
            }

            return version;
        }

        // ---------- draft editing ----------

        public Result UpdateDraft(string? title, string? description, int? credits, int? lectureHours, int? practicalHours, DateOnly effectiveFrom, DateTimeOffset now)
        {
            if (Status != CourseVersionStatus.Draft)
            {
                return Result.Fail(CoursesErrors.VersionNotDraft);
            }

            if (effectiveFrom == default)
            {
                return Result.Fail(CoursesErrors.EffectiveDateInvalid);
            }

            var applied = Apply(title, description, credits, lectureHours, practicalHours);
            if (applied.IsFailure)
            {
                return applied;
            }

            EffectiveFrom = effectiveFrom;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- lifecycle ----------

        // Draft -> Active. The caller retires the version this one replaces and checks the dates against it.
        public Result Activate(DateTimeOffset now)
        {
            if (Status != CourseVersionStatus.Draft)
            {
                return Result.Fail(CoursesErrors.VersionInvalidState);
            }

            Status = CourseVersionStatus.Active;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Active -> Retired, with the last day it is effective.
        public Result Retire(DateOnly effectiveTo, DateTimeOffset now)
        {
            if (Status != CourseVersionStatus.Active)
            {
                return Result.Fail(CoursesErrors.VersionInvalidState);
            }

            if (effectiveTo < EffectiveFrom)
            {
                return Result.Fail(CoursesErrors.EffectiveToInvalid);
            }

            Status = CourseVersionStatus.Retired;
            EffectiveTo = effectiveTo;
            UpdatedAt = now;
            return Result.Ok();
        }

        // ---------- questions ----------

        // True for an Active or Retired version whose window covers the date. A Draft is never effective.
        public bool IsEffectiveOn(DateOnly date)
            => Status != CourseVersionStatus.Draft && EffectiveFrom <= date && (EffectiveTo is null || date <= EffectiveTo);

        private Result Apply(string? title, string? description, int? credits, int? lectureHours, int? practicalHours)
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

            if (credits is not { } creditValue || creditValue < 1 || creditValue > MaxCredits)
            {
                return Result.Fail(CoursesErrors.CreditsInvalid);
            }

            if (lectureHours is not { } lecture || practicalHours is not { } practical
                || lecture < 0 || lecture > MaxHours || practical < 0 || practical > MaxHours || lecture + practical == 0)
            {
                return Result.Fail(CoursesErrors.HoursInvalid);
            }

            Title = cleanTitle;
            Description = cleanDescription;
            Credits = creditValue;
            LectureHours = lecture;
            PracticalHours = practical;
            return Result.Ok();
        }
    }
}
