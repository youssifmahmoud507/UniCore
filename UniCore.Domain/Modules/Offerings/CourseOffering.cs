using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Offerings
{
    // A course version offered in one semester (database index on CourseVersionId + AcademicSemesterId).
    // The version is the one valid at the semester start, so enrollments, grades and transcripts keep pointing at
    // it even after the course changes. The sections (CourseSection) are separate aggregates so that enrolling
    // students in one section never locks the offering or another section.
    public sealed class CourseOffering : AggregateRoot
    {
        public const int MaxSectionsLimit = 50;

        private CourseOffering()
        {
        }

        public Guid CourseVersionId { get; private set; }
        public Guid AcademicSemesterId { get; private set; }
        public CourseOfferingStatus Status { get; private set; }

        // Null means no limit.
        public int? MaxSections { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? UpdatedAt { get; private set; }

        // The application layer checks the course version, the semester and that the pair is not offered yet.
        public static Result<CourseOffering> Create(Guid courseVersionId, Guid academicSemesterId, int? maxSections, DateTimeOffset now)
        {
            if (courseVersionId == Guid.Empty || academicSemesterId == Guid.Empty)
            {
                return OfferingsErrors.InvalidParent;
            }

            if (!IsValidMax(maxSections))
            {
                return OfferingsErrors.MaxSectionsInvalid;
            }

            return new CourseOffering
            {
                Id = Guid.NewGuid(),
                CourseVersionId = courseVersionId,
                AcademicSemesterId = academicSemesterId,
                MaxSections = maxSections,
                Status = CourseOfferingStatus.Draft,
                CreatedAt = now
            };
        }

        // The caller passes how many sections are not cancelled; the limit cannot go below it.
        public Result ChangeMaxSections(int? maxSections, int activeSectionCount, DateTimeOffset now)
        {
            if (Status == CourseOfferingStatus.Cancelled)
            {
                return Result.Fail(OfferingsErrors.OfferingInvalidState);
            }

            if (!IsValidMax(maxSections) || (maxSections is { } max && max < activeSectionCount))
            {
                return Result.Fail(OfferingsErrors.MaxSectionsInvalid);
            }

            MaxSections = maxSections;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Draft or Closed -> Open. Needs at least one section that is not cancelled.
        public Result Open(int activeSectionCount, DateTimeOffset now)
        {
            if (Status != CourseOfferingStatus.Draft && Status != CourseOfferingStatus.Closed)
            {
                return Result.Fail(OfferingsErrors.OfferingInvalidState);
            }

            if (activeSectionCount < 1)
            {
                return Result.Fail(OfferingsErrors.OfferingNeedsSection);
            }

            Status = CourseOfferingStatus.Open;
            UpdatedAt = now;
            return Result.Ok();
        }

        public Result Close(DateTimeOffset now)
        {
            if (Status != CourseOfferingStatus.Open)
            {
                return Result.Fail(OfferingsErrors.OfferingInvalidState);
            }

            Status = CourseOfferingStatus.Closed;
            UpdatedAt = now;
            return Result.Ok();
        }

        // Final. The caller cancels the sections and checks that nobody is enrolled.
        public Result Cancel(DateTimeOffset now)
        {
            if (Status == CourseOfferingStatus.Cancelled)
            {
                return Result.Fail(OfferingsErrors.OfferingInvalidState);
            }

            Status = CourseOfferingStatus.Cancelled;
            UpdatedAt = now;
            return Result.Ok();
        }

        // True when students may be enrolled (registration asks the read service, which uses this).
        public bool AcceptsEnrollment => Status == CourseOfferingStatus.Open;

        private static bool IsValidMax(int? maxSections) => maxSections is null || (maxSections >= 1 && maxSections <= MaxSectionsLimit);
    }
}
