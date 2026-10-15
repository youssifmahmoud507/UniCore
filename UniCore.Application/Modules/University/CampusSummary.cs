namespace UniCore.Application.Modules.University
{
    public sealed record CampusSummary(Guid Id, Guid UniversityId, string Code, string Name, AddressSummary Address, string TimeZone, bool IsActive);
}
