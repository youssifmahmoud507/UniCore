namespace UniCore.Api.Contracts.Finance
{
    // ValidFrom = null means today.
    public sealed record AwardScholarshipRequest(Guid? StudentId, Guid? ScholarshipId, DateOnly? ValidFrom, DateOnly? ValidTo, string? Reason);
}
