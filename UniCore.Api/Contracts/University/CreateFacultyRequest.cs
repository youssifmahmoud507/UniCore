namespace UniCore.Api.Contracts.University
{
    public sealed record CreateFacultyRequest(Guid? UniversityId, string? Code, string? Name, string? Description, string? Email, string? Phone);
}
