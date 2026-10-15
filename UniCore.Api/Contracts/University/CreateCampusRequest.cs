namespace UniCore.Api.Contracts.University
{
    public sealed record CreateCampusRequest(Guid? UniversityId, string? Code, string? Name, AddressRequest? Address, string? TimeZone);
}
