namespace UniCore.Application.Modules.Reporting
{
    // Pending withdrawal and leave requests of the students of one department. Kind is "Withdrawal" or "Leave".
    public sealed record AcademicRequestStat(string Kind, Guid? DepartmentId, int Count);
}
