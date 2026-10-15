namespace UniCore.Application.Modules.University
{
    public sealed record CreateFacultyCommand(Guid UniversityId, string? Code, string? Name, string? Description, string? Email, string? Phone);
}
