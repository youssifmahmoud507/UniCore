namespace UniCore.Application.Modules.University
{
    public sealed record CreateDepartmentCommand(Guid FacultyId, string? Code, string? Name, string? Description, string? Email, string? Phone);
}
