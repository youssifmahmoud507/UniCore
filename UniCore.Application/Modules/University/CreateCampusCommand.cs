namespace UniCore.Application.Modules.University
{
    public sealed record CreateCampusCommand(Guid UniversityId, string? Code, string? Name, AddressInput? Address, string? TimeZone);
}
