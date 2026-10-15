namespace UniCore.Api.Contracts.University
{
    public sealed record UpdateCampusRequest(string? Name, AddressRequest? Address, string? TimeZone);
}
