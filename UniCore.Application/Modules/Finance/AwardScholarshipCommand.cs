namespace UniCore.Application.Modules.Finance
{
    // ValidFrom = null means today.
    public sealed record AwardScholarshipCommand(Guid? StudentId, Guid? ScholarshipId, DateOnly? ValidFrom, DateOnly? ValidTo, string? Reason);
}
