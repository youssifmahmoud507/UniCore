using UniCore.Application.Modules.Documents;
using UniCore.Domain.Modules.Attendance;

namespace UniCore.Application.Modules.Attendance
{
    // Exactly one of AcademicProgramId / CourseId.
    public sealed record CreatePolicyCommand(Guid? AcademicProgramId, Guid? CourseId, decimal MinimumPercentage, decimal WarningPercentage, int LateThresholdMinutes, bool IsExamBlocking);

    public sealed record UpdatePolicyCommand(decimal MinimumPercentage, decimal WarningPercentage, int LateThresholdMinutes, bool IsExamBlocking);

    public sealed record OpenSessionCommand(Guid CourseSectionId, DateOnly SessionDate, TimeOnly StartTime);

    public sealed record StudentMark(Guid StudentId, AttendanceStatus Status, int? MinutesLate);

    public sealed record MarkAttendanceCommand(IReadOnlyList<StudentMark> Marks);

    // The file is optional (stored through IDocumentService, type ATTENDANCE_EXCUSE). The reason is required.
    public sealed record SubmitExcuseCommand(Guid AttendanceRecordId, string? Reason, DocumentUpload? File);

    public sealed record ReviewExcuseCommand(bool Accept, string? Note);
}
