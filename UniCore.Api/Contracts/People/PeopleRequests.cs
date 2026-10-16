using UniCore.Domain.Modules.People;

namespace UniCore.Api.Contracts.People
{
    public sealed record PersonRequest(string? FirstName, string? LastName, DateOnly? DateOfBirth, Gender? Gender, string? NationalId, string? Email, string? Phone);

    public sealed record LinkUserRequest(Guid? UserId);

    public sealed record CreateEmployeeRequest(Guid? PersonProfileId, string? EmployeeNumber, Guid? DepartmentId, string? JobTitle, DateOnly? HireDate);

    public sealed record UpdateEmployeeRequest(Guid? DepartmentId, string? JobTitle);

    public sealed record TerminateEmployeeRequest(DateOnly? TerminationDate);

    public sealed record CreateInstructorRequest(Guid? EmployeeId, AcademicRank? Rank, string? Specialization);

    public sealed record UpdateInstructorRequest(AcademicRank? Rank, string? Specialization);

    public sealed record CreateTeachingAssistantRequest(Guid? EmployeeId, Guid? StudentId);
}
