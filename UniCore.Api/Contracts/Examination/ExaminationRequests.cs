using UniCore.Domain.Modules.Examination;

namespace UniCore.Api.Contracts.Examination
{
    // Ids, dates, times and numbers are nullable so a missing value is reported as a validation error instead of silently becoming a default.
    // Dates "yyyy-MM-dd", times "HH:mm".
    public sealed record CreateExamSessionRequest(Guid? AcademicSemesterId, string? Name, ExamType? ExamType, DateOnly? StartDate, DateOnly? EndDate);

    public sealed record CreateExamRequest(Guid? CourseSectionId, Guid? ExamSessionId, ExamType? ExamType, decimal? MaxScore, int? DurationMinutes);

    public sealed record UpdateExamRequest(decimal? MaxScore, int? DurationMinutes);

    public sealed record SetExamScheduleRequest(DateOnly? ExamDate, TimeOnly? StartTime, TimeOnly? EndTime);

    public sealed record AddExamRoomRequest(Guid? RoomId);

    public sealed record AddInvigilatorRequest(Guid? RoomId, Guid? InstructorId, InvigilatorRole? Role);
}
