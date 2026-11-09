using UniCore.Domain.Modules.Examination;

namespace UniCore.Application.Modules.Examination
{
    public sealed record CreateExamSessionCommand(Guid AcademicSemesterId, string? Name, ExamType ExamType, DateOnly StartDate, DateOnly EndDate);

    public sealed record CreateExamCommand(Guid CourseSectionId, Guid ExamSessionId, ExamType ExamType, decimal MaxScore, int DurationMinutes);

    public sealed record UpdateExamCommand(decimal MaxScore, int DurationMinutes);

    public sealed record SetExamScheduleCommand(DateOnly ExamDate, TimeOnly StartTime, TimeOnly EndTime);

    public sealed record AddExamRoomCommand(Guid RoomId);

    public sealed record AddInvigilatorCommand(Guid RoomId, Guid InstructorId, InvigilatorRole Role);
}
