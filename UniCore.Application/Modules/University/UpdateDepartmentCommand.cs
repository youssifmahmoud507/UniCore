namespace UniCore.Application.Modules.University
{
    public sealed record UpdateDepartmentCommand(string? Name, string? Description, string? Email, string? Phone);
}
