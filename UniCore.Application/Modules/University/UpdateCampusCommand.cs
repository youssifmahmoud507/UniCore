namespace UniCore.Application.Modules.University
{
    public sealed record UpdateCampusCommand(string? Name, AddressInput? Address, string? TimeZone);
}
