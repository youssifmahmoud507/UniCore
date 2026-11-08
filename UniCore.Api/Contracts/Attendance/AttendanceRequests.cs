using UniCore.Domain.Modules.Attendance;

namespace UniCore.Api.Contracts.Attendance
{
    // Ids, dates, times and numbers are nullable so a missing value is reported as a validation error instead of silently becoming a default.
    // Exactly one of AcademicProgramId / CourseId.
    public sealed record CreatePolicyRequest(Guid? AcademicProgramId, Guid? CourseId, decimal? MinimumPercentage, decimal? WarningPercentage, int? LateThresholdMinutes, bool? IsExamBlocking);

    public sealed record UpdatePolicyRequest(decimal? MinimumPercentage, decimal? WarningPercentage, int? LateThresholdMinutes, bool? IsExamBlocking);

    // Date "yyyy-MM-dd", time "HH:mm".
    public sealed record OpenSessionRequest(Guid? CourseSectionId, DateOnly? SessionDate, TimeOnly? StartTime);

    public sealed record MarkItemRequest(Guid? StudentId, AttendanceStatus? Status, int? MinutesLate);

    public sealed record MarkAttendanceRequest(List<MarkItemRequest>? Marks);

    // multipart/form-data: AttendanceRecordId, Reason and an optional File (pdf/image, checked by the Documents module).
    public sealed class SubmitExcuseForm
    {
        public Guid? AttendanceRecordId { get; set; }
        public string? Reason { get; set; }
        public IFormFile? File { get; set; }
    }

    public sealed record ReviewExcuseRequest(bool? Accept, string? Note);
}
