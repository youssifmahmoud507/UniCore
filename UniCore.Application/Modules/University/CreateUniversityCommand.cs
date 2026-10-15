namespace UniCore.Application.Modules.University
{
    public sealed record CreateUniversityCommand(string? Code, string? Name, string? Description, string? Email, string? Phone, string? Website);
}
