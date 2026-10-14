using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Students
{
    // Raised by Student.Create. The outbox delivers it after the student is saved.
    public sealed record StudentAdmitted(Guid EventId, DateTimeOffset OccurredAt, Guid StudentId, Guid PersonProfileId, string StudentNumber, Guid AcademicProgramId, Guid StudyPlanVersionId, Guid AdmissionAcademicYearId, DateOnly AdmissionDate) : IDomainEvent;
}
