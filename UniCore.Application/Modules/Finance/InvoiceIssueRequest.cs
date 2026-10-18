namespace UniCore.Application.Modules.Finance
{
    public sealed record InvoiceIssueRequest(Guid StudentId, Guid? AcademicSemesterId, DateOnly? DueDate, IReadOnlyList<Guid>? DiscountIds, IReadOnlyList<Guid>? ChargeIds);

}
