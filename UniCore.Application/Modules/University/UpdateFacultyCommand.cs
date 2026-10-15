namespace UniCore.Application.Modules.University
{
    public sealed record UpdateFacultyCommand(string? Name, string? Description, string? Email, string? Phone);
}
