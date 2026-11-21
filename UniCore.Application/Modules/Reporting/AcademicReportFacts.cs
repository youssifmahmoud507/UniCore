using UniCore.Domain.Modules.Registrations;

namespace UniCore.Application.Modules.Reporting
{
    // The academic reports read already-counted groups (GROUP BY in the database), never raw rows.
    // Every fact sits in the department of the course, so the user's scope decides which ones are shown.
    public sealed record EnrollmentStat(Guid DepartmentId, string DepartmentName, EnrollmentStatus Status, int Enrollments, int Credits);

    public sealed record GradeStat(Guid DepartmentId, string DepartmentName, string Letter, int Grades, decimal PercentageSum);

    // One row per student: grade points x credits and credits of the student's Published/Locked grades.
    public sealed record StudentGpaStat(Guid DepartmentId, string DepartmentName, decimal QualityPoints, int Credits);

    public sealed record AttendanceStat(Guid DepartmentId, string DepartmentName, string Status, int Records);
}
