namespace UniCore.Domain.Modules.Attendance
{
    // How a student was recorded in a session. Excused is reached only through an accepted excuse, never marked directly. Stored as text.
    public enum AttendanceStatus
    {
        Present,
        Absent,
        Late,
        Excused
    }

    // Open -> Closed. Marking is allowed only while Open; only Closed sessions count in the percentage. Stored as text.
    public enum AttendanceSessionStatus
    {
        Open,
        Closed
    }

    // Pending -> Accepted or Rejected (final). Stored as text.
    public enum ExcuseStatus
    {
        Pending,
        Accepted,
        Rejected
    }

    // Warning: below WarningPercentage. Critical: below MinimumPercentage. Stored as text.
    public enum WarningLevel
    {
        Warning,
        Critical
    }
}
