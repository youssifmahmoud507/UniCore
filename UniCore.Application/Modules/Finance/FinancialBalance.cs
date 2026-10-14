namespace UniCore.Application.Modules.Finance
{
    // Contract for Developer 1 (Registration, Graduation, Services).
    // Outstanding = what the student still owes (negative means the student has credit).
    public sealed record FinancialBalance(Guid StudentId, decimal TotalCharged, decimal TotalPaid, decimal Outstanding);

}
