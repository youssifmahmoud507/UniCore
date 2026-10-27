using UniCore.Domain.Common.Entities;
using UniCore.Domain.Common.Results;

namespace UniCore.Domain.Modules.Offerings
{
    // An instructor teaching a section in a role, from AssignedAt until EndedAt (end-dating keeps the history:
    // a row is never deleted or reused). Replaces a single InstructorId column on the section.
    // Rules kept by the application layer and by filtered unique indexes: an instructor has at most one active
    // assignment per section, and a section has at most one active Primary.
    public sealed class InstructorAssignment : AggregateRoot
    {
        private InstructorAssignment()
        {
        }

        public Guid CourseSectionId { get; private set; }
        public Guid InstructorId { get; private set; }
        public AssignmentRole Role { get; private set; }
        public DateTimeOffset AssignedAt { get; private set; }
        public DateTimeOffset? EndedAt { get; private set; }

        public bool IsActive => EndedAt is null;

        // The application layer checks the section, the instructor and the two rules above.
        public static Result<InstructorAssignment> Create(Guid courseSectionId, Guid instructorId, AssignmentRole? role, DateTimeOffset now)
        {
            if (courseSectionId == Guid.Empty || instructorId == Guid.Empty || role is not { } value || !Enum.IsDefined(value))
            {
                return OfferingsErrors.AssignmentInvalid;
            }

            return new InstructorAssignment
            {
                Id = Guid.NewGuid(),
                CourseSectionId = courseSectionId,
                InstructorId = instructorId,
                Role = value,
                AssignedAt = now
            };
        }

        public Result End(DateTimeOffset now)
        {
            if (EndedAt is not null)
            {
                return Result.Fail(OfferingsErrors.AssignmentAlreadyEnded);
            }

            EndedAt = now < AssignedAt ? AssignedAt : now;
            return Result.Ok();
        }
    }
}
