using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;
using UniCore.Domain.Common.Validation;

namespace UniCore.Domain.Modules.Offerings
{
    // One class group of an offering ("A", "B", "L1"). Unique code inside the offering (database index on
    // CourseOfferingId + SectionCode). It is its own aggregate with a RowVersion, because registration changes the
    // enrolled count of a section concurrently (Week 9). Instructors are InstructorAssignment rows, not a column.
    public sealed class CourseSection : AggregateRoot
    {
        public const int CodeMaxLength = 10;
        public const int MaxCapacity = 1000;

        private CourseSection()
        {
        }

        public Guid CourseOfferingId { get; private set; }

        // Upper-case, never changed.
        public string SectionCode { get; private set; } = string.Empty;

        public int Capacity { get; private set; }
        public CourseSectionStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks the offering (not cancelled, section limit) and that the code is free.
        public static Result<CourseSection> Create(Guid courseOfferingId, string? sectionCode, int? capacity, DateTimeOffset now)
        {
            if (courseOfferingId == Guid.Empty)
            {
                return OfferingsErrors.InvalidParent;
            }

            var cleanCode = sectionCode?.Trim().ToUpperInvariant();
            if (cleanCode is null || !FieldRules.IsValidCode(cleanCode, 1, CodeMaxLength))
            {
                return OfferingsErrors.SectionCodeInvalid;
            }

            if (capacity is not { } size || !IsValidCapacity(size))
            {
                return OfferingsErrors.CapacityInvalid;
            }

            return new CourseSection
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = courseOfferingId,
                SectionCode = cleanCode,
                Capacity = size,
                Status = CourseSectionStatus.Open,
                CreatedAt = now
            };
        }

        // The caller passes the number of enrolled students; the capacity cannot go below it.
        public Result ChangeCapacity(int? capacity, int enrolledCount, DateTimeOffset now)
        {
            if (Status == CourseSectionStatus.Cancelled)
            {
                return Result.Fail(OfferingsErrors.SectionInvalidState);
            }

            if (capacity is not { } size || !IsValidCapacity(size))
            {
                return Result.Fail(OfferingsErrors.CapacityInvalid);
            }

            if (size < enrolledCount)
            {
                return Result.Fail(OfferingsErrors.CapacityBelowEnrolled);
            }

            Capacity = size;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Close(DateTimeOffset now)
        {
            if (Status != CourseSectionStatus.Open)
            {
                return Result.Fail(OfferingsErrors.SectionInvalidState);
            }

            Status = CourseSectionStatus.Closed;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Reopen(DateTimeOffset now)
        {
            if (Status != CourseSectionStatus.Closed)
            {
                return Result.Fail(OfferingsErrors.SectionInvalidState);
            }

            Status = CourseSectionStatus.Open;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Final. Only while nobody is enrolled (the caller passes the count).
        public Result Cancel(int enrolledCount, DateTimeOffset now)
        {
            if (Status == CourseSectionStatus.Cancelled)
            {
                return Result.Fail(OfferingsErrors.SectionInvalidState);
            }

            if (enrolledCount > 0)
            {
                return Result.Fail(OfferingsErrors.SectionHasEnrollments);
            }

            Status = CourseSectionStatus.Cancelled;
            UpdatedAt = now;
            return Result.Ok();
        }

        public bool IsOpen => Status == CourseSectionStatus.Open;

        private static bool IsValidCapacity(int capacity) => capacity is >= 1 and <= MaxCapacity;
    }
}
