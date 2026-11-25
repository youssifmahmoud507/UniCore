using UniCore.Domain.Modules.Students;

namespace UniCore.Application.Modules.Reporting
{
    // Students of one academic status in one department (the department of the current program; null for a student
    // without a program).
    public sealed record StudentStatusStat(StudentAcademicStatus Status, Guid? DepartmentId, int Count);
}
