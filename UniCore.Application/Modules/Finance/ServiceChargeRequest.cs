namespace UniCore.Application.Modules.Finance
{
    // FeeCode: the code of the fee in the catalog (e.g. TRANSCRIPT). Reference: the id of the request in the caller's module.
    // Amount = null means the default amount of the fee. DueDate = null means 14 days from today.
    public sealed record ServiceChargeRequest(Guid StudentId, string? FeeCode, string? Reference, DateOnly? DueDate = null, decimal? Amount = null);

}
