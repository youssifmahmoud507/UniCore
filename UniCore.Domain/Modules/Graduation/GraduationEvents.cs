using UniCore.Domain.Common.Events;
using UniCore.Domain.Modules.Students;

namespace UniCore.Domain.Modules.Graduation
{
    // Raised when a graduation application is approved (the student's status becomes Graduated in the same save).
    // Consumers (Developer 2): Notification tells the student (W19), Documents may issue the certificate (W20), reports count graduates.
    // Consumers must be idempotent, keyed by EventId. Standing is the academic standing at the time of approval.
    public sealed record GraduationApproved(
        Guid EventId, DateTimeOffset OccurredAt, Guid ApplicationId, Guid StudentId, Guid AcademicProgramId, Guid StudyPlanVersionId,
        decimal CumulativeGpa, int EarnedCredits, AcademicStanding? Standing, DateOnly GraduationDate, Guid ApprovedByUserId) : IDomainEvent;
}
