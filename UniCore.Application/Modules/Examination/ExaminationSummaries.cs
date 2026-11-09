using UniCore.Domain.Modules.Examination;

namespace UniCore.Application.Modules.Examination
{
    public sealed record ExamSessionSummary(Guid Id, Guid AcademicSemesterId, string Name, ExamType ExamType, DateOnly StartDate, DateOnly EndDate, ExamSessionStatus Status, DateTimeOffset CreatedAt);

    public sealed record ExamScheduleSummary(DateOnly ExamDate, TimeOnly StartTime, TimeOnly EndTime);

    public sealed record ExamRoomSummary(Guid RoomId, int Capacity);

    public sealed record InvigilatorSummary(Guid InstructorId, Guid RoomId, InvigilatorRole Role);

    public sealed record ExamSummary(
        Guid Id, Guid CourseSectionId, Guid ExamSessionId, ExamType ExamType, decimal MaxScore, int DurationMinutes, ExamStatus Status,
        ExamScheduleSummary? Schedule, int TotalCapacity, IReadOnlyList<ExamRoomSummary> Rooms, IReadOnlyList<InvigilatorSummary> Invigilators,
        DateTimeOffset CreatedAt, DateTimeOffset? PublishedAt);

    // Registered students of the exam's section, split by the attendance rule (Week 11): blocked students are not on the exam list.
    public sealed record ExamRosterSummary(Guid ExamId, int Registered, IReadOnlyList<Guid> EligibleStudentIds, IReadOnlyList<Guid> BlockedStudentIds);

    public static class ExaminationMapper
    {
        public static ExamSessionSummary ToSummary(ExamSession s) => new(s.Id, s.AcademicSemesterId, s.Name, s.ExamType, s.StartDate, s.EndDate, s.Status, s.CreatedAt);

        public static ExamSummary ToSummary(Exam e)
            => new(
                e.Id, e.CourseSectionId, e.ExamSessionId, e.ExamType, e.MaxScore, e.DurationMinutes, e.Status,
                e.Schedule is null ? null : new ExamScheduleSummary(e.Schedule.ExamDate, e.Schedule.StartTime, e.Schedule.EndTime),
                e.TotalCapacity,
                [.. e.Rooms.Select(r => new ExamRoomSummary(r.RoomId, r.Capacity))],
                [.. e.Invigilators.Select(i => new InvigilatorSummary(i.InstructorId, e.Rooms.First(r => r.Id == i.ExamRoomId).RoomId, i.Role))],
                e.CreatedAt, e.PublishedAt);
    }
}
