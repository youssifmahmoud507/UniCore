namespace UniCore.Application.Modules.University
{
    public sealed record DepartmentSummary(Guid Id, Guid FacultyId, string Code, string Name, string? Description, string? Email, string? Phone, bool IsActive);
}
