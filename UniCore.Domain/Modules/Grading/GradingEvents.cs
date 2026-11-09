using UniCore.Domain.Common.Events;

namespace UniCore.Domain.Modules.Grading
{
    // Raised when a grade is published. Notification tells the student; GPA (Week 15) counts it. Consumers must be idempotent, keyed by EventId.
    public sealed record GradePublished(
        Guid EventId, DateTimeOffset OccurredAt, Guid GradeId, Guid EnrollmentId, Guid StudentId, Guid CourseSectionId, Guid AcademicSemesterId,
        Guid CourseId, int Credits, decimal Percentage, string Letter, decimal GradePoints, bool IsPassing) : IDomainEvent;

    // Raised when a published or locked grade is corrected through an approved change. GPA recalculates, the student is told.
    public sealed record GradeCorrected(
        Guid EventId, DateTimeOffset OccurredAt, Guid GradeId, Guid EnrollmentId, Guid StudentId, Guid CourseSectionId, Guid AcademicSemesterId,
        Guid CourseId, int Credits, decimal OldPercentage, string OldLetter, decimal NewPercentage, string NewLetter, decimal NewGradePoints, bool NewIsPassing) : IDomainEvent;
}
