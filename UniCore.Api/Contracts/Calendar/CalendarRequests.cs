using UniCore.Domain.Modules.Calendar;

namespace UniCore.Api.Contracts.Calendar
{
    // Required fields are nullable here so that a missing value reaches the domain and comes back as a
    // validation error (a missing date becomes the default date, which the domain rejects).
    public sealed record AcademicYearRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate);

    public sealed record CreateAcademicTermRequest(string? Code, string? Name, int? SortOrder);

    public sealed record UpdateAcademicTermRequest(string? Name, int? SortOrder);

    public sealed record SemesterWindowsRequest(
        DateOnly? RegistrationStart, DateOnly? RegistrationEnd, DateOnly? AddDropStart, DateOnly? AddDropEnd,
        DateOnly? WithdrawalDeadline, DateOnly? MidtermStart, DateOnly? MidtermEnd, DateOnly? FinalExamStart,
        DateOnly? FinalExamEnd, DateOnly? GradeSubmissionDeadline, DateOnly? GradePublicationDate)
    {
        public SemesterWindows ToWindows() => new(
            RegistrationStart, RegistrationEnd, AddDropStart, AddDropEnd, WithdrawalDeadline, MidtermStart, MidtermEnd,
            FinalExamStart, FinalExamEnd, GradeSubmissionDeadline, GradePublicationDate);
    }

    public sealed record CreateSemesterRequest(Guid? AcademicYearId, Guid? AcademicTermId, DateOnly? StartDate, DateOnly? EndDate, SemesterWindowsRequest? Windows);

    public sealed record UpdateSemesterRequest(DateOnly? StartDate, DateOnly? EndDate, SemesterWindowsRequest? Windows);

    public sealed record AdvanceSemesterRequest(SemesterStatus? Status);

    public sealed record CreateAcademicLevelRequest(Guid? AcademicProgramId, string? Name, int? Order, int? RequiredCredits);

    public sealed record UpdateAcademicLevelRequest(string? Name, int? Order, int? RequiredCredits);
}
