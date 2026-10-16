using UniCore.Domain.Modules.People;

namespace UniCore.Application.Modules.People
{
    // NationalId is null unless the caller holds People.Manage.
    public sealed record PersonSummary(Guid Id, string FirstName, string LastName, DateOnly? DateOfBirth, Gender? Gender, string? NationalId, string? Email, string? Phone, Guid? UserId, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record EmployeeSummary(Guid Id, Guid PersonProfileId, string EmployeeNumber, Guid? DepartmentId, string? JobTitle, DateOnly HireDate, DateOnly? TerminationDate, EmployeeStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record InstructorSummary(Guid Id, Guid EmployeeId, AcademicRank Rank, string? Specialization, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record TeachingAssistantSummary(Guid Id, Guid? EmployeeId, Guid? StudentId, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

    public sealed record CreatePersonCommand(string? FirstName, string? LastName, DateOnly? DateOfBirth, Gender? Gender, string? NationalId, string? Email, string? Phone);

    public sealed record UpdatePersonCommand(string? FirstName, string? LastName, DateOnly? DateOfBirth, Gender? Gender, string? NationalId, string? Email, string? Phone);

    public sealed record CreateEmployeeCommand(Guid PersonProfileId, string? EmployeeNumber, Guid? DepartmentId, string? JobTitle, DateOnly HireDate);

    public sealed record UpdateEmployeeCommand(Guid? DepartmentId, string? JobTitle);

    public sealed record CreateInstructorCommand(Guid EmployeeId, AcademicRank? Rank, string? Specialization);

    public sealed record UpdateInstructorCommand(AcademicRank? Rank, string? Specialization);

    public sealed record CreateTeachingAssistantCommand(Guid? EmployeeId, Guid? StudentId);

    public static class PeopleMapper
    {
        public static PersonSummary ToSummary(PersonProfile p, bool includeNationalId)
            => new(p.Id, p.FirstName, p.LastName, p.DateOfBirth, p.Gender, includeNationalId ? p.NationalId : null, p.Email, p.Phone, p.UserId, p.CreatedAt, p.UpdatedAt);

        public static EmployeeSummary ToSummary(Employee e)
            => new(e.Id, e.PersonProfileId, e.EmployeeNumber, e.DepartmentId, e.JobTitle, e.HireDate, e.TerminationDate, e.Status, e.CreatedAt, e.UpdatedAt);

        public static InstructorSummary ToSummary(Instructor i)
            => new(i.Id, i.EmployeeId, i.Rank, i.Specialization, i.IsActive, i.CreatedAt, i.UpdatedAt);

        public static TeachingAssistantSummary ToSummary(TeachingAssistant t)
            => new(t.Id, t.EmployeeId, t.StudentId, t.IsActive, t.CreatedAt, t.UpdatedAt);
    }
}
