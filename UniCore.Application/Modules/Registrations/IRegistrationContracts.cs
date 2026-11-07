using UniCore.Domain.Common.Results;

namespace UniCore.Application.Modules.Registrations
{
    // ---------------------------------------------------------------------------------------------
    // Contracts the registration pipeline needs from rules that other weeks / the other developer own.
    // Infrastructure registers a safe default for each one (see Modules/Registrations/Defaults) until the real
    // implementation arrives; the pipeline does not change when it does.
    // ---------------------------------------------------------------------------------------------

    // Prerequisites and corequisites of a course version (Developer 2 / Curriculum rules).
    public interface IPrerequisiteEvaluator
    {
        // True when the student satisfies every prerequisite of the course version (passed courses, grades, levels).
        Task<Result<bool>> ArePrerequisitesMetAsync(Guid studentId, Guid courseVersionId, CancellationToken cancellationToken = default);

        // Course ids (not versions) that must be passed OR taken in the same semester.
        Task<Result<IReadOnlyList<Guid>>> GetCorequisiteCourseIdsAsync(Guid courseVersionId, CancellationToken cancellationToken = default);
    }

    // What the student has already done in a course. Week 14 (grading) replaces the default.
    public sealed record CourseAttemptSummary(bool Passed, decimal? BestGradePoint, int Attempts);

    public interface IStudentCourseHistory
    {
        Task<Result<CourseAttemptSummary>> GetAttemptsAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default);
    }

    // Week 10 (timetable) replaces the default.
    public interface IScheduleConflictService
    {
        // True when the section overlaps one of the sections the student already holds this semester.
        Task<Result<bool>> HasConflictAsync(Guid studentId, Guid semesterId, Guid courseSectionId, IReadOnlyCollection<Guid> enrolledSectionIds, CancellationToken cancellationToken = default);
    }

    // Tunable numbers. Infrastructure reads them from configuration (keys Registration:*).
    public interface IRegistrationSettings
    {
        int RegularCreditLimit { get; }
        int ProbationCreditLimit { get; }
        int SummerCreditLimit { get; }

        // The AcademicTerm code that means a summer semester.
        string SummerTermCode { get; }

        // A passed course may be taken again to improve the grade, while the best grade point is below the maximum.
        bool AllowGradeImprovement { get; }
        decimal ImprovementMaxGradePoint { get; }
    }
}
