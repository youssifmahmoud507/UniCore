namespace UniCore.Application.Modules.University
{
    // The code never changes, so it is not part of an update.
    public sealed record UpdateUniversityCommand(string? Name, string? Description, string? Email, string? Phone, string? Website);
}
