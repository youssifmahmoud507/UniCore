namespace UniCore.Api.Contracts.University
{
    public sealed record CreateDepartmentRequest(Guid? FacultyId, string? Code, string? Name, string? Description, string? Email, string? Phone);
}
