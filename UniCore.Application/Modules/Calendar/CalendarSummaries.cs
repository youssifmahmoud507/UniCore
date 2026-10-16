using UniCore.Domain.Modules.Calendar;

namespace UniCore.Application.Modules.Calendar
{
    public sealed record AcademicYearSummary(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, AcademicYearStatus Status, bool IsCurrent, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record AcademicTermSummary(Guid Id, string Code, string Name, int SortOrder, bool IsActive);

    public sealed record SemesterSummary(Guid Id, Guid AcademicYearId, Guid AcademicTermId, DateOnly StartDate, DateOnly EndDate, SemesterStatus Status, SemesterWindows Windows, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record AcademicLevelSummary(Guid Id, Guid AcademicProgramId, string Name, int Order, int RequiredCredits, bool IsActive);

    public sealed record CreateAcademicYearCommand(string? Name, DateOnly StartDate, DateOnly EndDate);

    public sealed record UpdateAcademicYearCommand(string? Name, DateOnly StartDate, DateOnly EndDate);

    public sealed record CreateAcademicTermCommand(string? Code, string? Name, int SortOrder);

    public sealed record UpdateAcademicTermCommand(string? Name, int SortOrder);

    public sealed record CreateSemesterCommand(Guid AcademicYearId, Guid AcademicTermId, DateOnly StartDate, DateOnly EndDate, SemesterWindows? Windows);

    public sealed record UpdateSemesterCommand(DateOnly StartDate, DateOnly EndDate, SemesterWindows? Windows);

    public sealed record CreateAcademicLevelCommand(Guid AcademicProgramId, string? Name, int Order, int RequiredCredits);

    public sealed record UpdateAcademicLevelCommand(string? Name, int Order, int RequiredCredits);

    public static class CalendarMapper
    {
        public static AcademicYearSummary ToSummary(AcademicYear y)
            => new(y.Id, y.Name, y.StartDate, y.EndDate, y.Status, y.IsCurrent, y.CreatedAt, y.UpdatedAt);

        public static AcademicTermSummary ToSummary(AcademicTerm t) => new(t.Id, t.Code, t.Name, t.SortOrder, t.IsActive);

        public static SemesterSummary ToSummary(AcademicSemester s)
            => new(s.Id, s.AcademicYearId, s.AcademicTermId, s.StartDate, s.EndDate, s.Status, s.Windows, s.CreatedAt, s.UpdatedAt);

        public static AcademicLevelSummary ToSummary(AcademicLevel l) => new(l.Id, l.AcademicProgramId, l.Name, l.Order, l.RequiredCredits, l.IsActive);
    }
}
